using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ForexExchange.Models;

namespace ForexExchange.Services
{
    public interface ISqlBackupService
    {
        Task<byte[]> ExportDataSqlAsync(CancellationToken ct = default);
        Task ImportDataSqlAsync(Stream sqlStream, CancellationToken ct = default);
    }

    /// <summary>
    /// SQLite data-only backup/restore via SQL scripts.
    /// - Export: generates a SQL script with DELETE + INSERT statements for all user tables (excluding sqlite_* tables).
    /// - Import: executes provided SQL script inside a single transaction with foreign_keys disabled to avoid FK temporary violations.
    /// </summary>
    public class SqliteSqlBackupService : ISqlBackupService
    {
        private readonly ForexDbContext _db;
        private readonly ILogger<SqliteSqlBackupService> _logger;

        public SqliteSqlBackupService(ForexDbContext db, ILogger<SqliteSqlBackupService> logger)
        {
            _db = db;
            _logger = logger;
        }

    public async Task<byte[]> ExportDataSqlAsync(CancellationToken ct = default)
        {
            var cs = _db.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("Database connection string is not configured");

            // Ensure shared cache; busy timeout will be set via PRAGMA
            if (!cs.Contains("Cache=Shared", StringComparison.OrdinalIgnoreCase))
                cs = cs.TrimEnd(';') + ";Cache=Shared";

            await using var conn = new SqliteConnection(cs);
            await conn.OpenAsync(ct);
            // Set busy timeout via PRAGMA instead of connection string
            await using (var busy = conn.CreateCommand())
            {
                busy.CommandText = "PRAGMA busy_timeout=5000;";
                await busy.ExecuteNonQueryAsync(ct);
            }

            // Get user tables from sqlite_master, excluding internal tables
            var tables = new List<string>();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var name = reader.GetString(0);
                    tables.Add(name);
                }
            }

            // Optional: keep a stable order to reduce FK issues on restore (we disable FK anyway)
            // Move __EFMigrationsHistory near the top for determinism
            tables = tables
                .OrderBy(t => t.Equals("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(t => t)
                .ToList();

            // Exclude seed-stable tables so backup/restore doesn't affect them
            // You can adjust this list if business rules change.
            var excludeTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "__EFMigrationsHistory", // meta
                "Currencies",            // seeded in OnModelCreating
                "ExchangeRates",         // initial has-data; rates also dynamic but excluded per request
                "CurrencyPools",         // initial has-data
                "SystemSettings"         // seeded defaults
            };

            var sb = new StringBuilder(1024 * 1024);
            sb.AppendLine("PRAGMA foreign_keys=OFF;");
            sb.AppendLine("BEGIN TRANSACTION;");

            foreach (var table in tables)
            {
                // Skip internal EF tables if desired. Here we include __EFMigrationsHistory.
                if (string.Equals(table, "sqlite_sequence", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (excludeTables.Contains(table))
                    continue; // excluded seeded tables

                // Retrieve columns for this table
                var columns = await GetTableColumnsAsync(conn, table, ct);
                if (columns.Count == 0) continue;

                // Select all data
                await using var dataCmd = conn.CreateCommand();
                dataCmd.CommandText = $"SELECT {string.Join(", ", columns.Select(c => "[" + EscapeIdent(c.Name) + "]"))} FROM [" + EscapeIdent(table) + "]";
                await using var dataReader = await dataCmd.ExecuteReaderAsync(ct);

                while (await dataReader.ReadAsync(ct))
                {
                    var values = new string[columns.Count];
                    for (int i = 0; i < columns.Count; i++)
                    {
                        values[i] = ToSqlLiteral(dataReader.GetValue(i), columns[i].Type);
                    }

                    // Append-only semantics; avoid overriding existing rows on PK/unique conflicts
                    sb.Append("INSERT OR IGNORE INTO [").Append(EscapeIdent(table)).Append("] (");
                    sb.Append(string.Join(", ", columns.Select(c => "[" + EscapeIdent(c.Name) + "]")));
                    sb.Append(") VALUES (");
                    sb.Append(string.Join(", ", values));
                    sb.AppendLine(");");
                }
            }

            sb.AppendLine("COMMIT;");
            sb.AppendLine("PRAGMA foreign_keys=ON;");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            _logger.LogInformation("Exported data SQL of length {Length} bytes", bytes.Length);
            return bytes;
        }

    public async Task ImportDataSqlAsync(Stream sqlStream, CancellationToken ct = default)
        {
            using var reader = new StreamReader(sqlStream, Encoding.UTF8, true, 1024, leaveOpen: true);
            var script = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(script))
                throw new InvalidOperationException("Uploaded SQL script is empty");

            // Wrap with pragmas and a single transaction if not present to be robust
            var wrapped = new StringBuilder(script.Length + 256);
            wrapped.AppendLine("PRAGMA foreign_keys=OFF;");
            wrapped.AppendLine("BEGIN TRANSACTION;");
            wrapped.AppendLine(script);
            wrapped.AppendLine("COMMIT;");
            wrapped.AppendLine("PRAGMA foreign_keys=ON;");

            // Execute via EF to run multiple statements safely
            await _db.Database.ExecuteSqlRawAsync(wrapped.ToString());
            _db.ChangeTracker.Clear();
            _logger.LogInformation("Data SQL import completed");
        }

        private static string EscapeIdent(string name)
        {
            // Bracket quoting is used for identifiers; still guard against "]" by doubling
            return name.Replace("]", "]]", StringComparison.Ordinal);
        }

        private static string ToSqlLiteral(object? value, string? declaredType)
        {
            if (value == null || value is DBNull) return "NULL";

            // Normalize declared type for simple handling
            var type = declaredType?.ToUpperInvariant() ?? string.Empty;

            switch (value)
            {
                case bool b:
                    return b ? "1" : "0";
                case byte or sbyte or short or ushort or int or uint or long or ulong:
                    return Convert.ToString(value, CultureInfo.InvariantCulture)!;
                case float or double or decimal:
                    return Convert.ToString(value, CultureInfo.InvariantCulture)!;
                case byte[] bytes:
                    // Store as X'ABCD' blob literal
                    var hex = BitConverter.ToString(bytes).Replace("-", string.Empty, StringComparison.Ordinal);
                    return $"X'{hex}'";
                case DateTime dt:
                    // Use ISO 8601 string literal
                    var iso = dt.ToString("yyyy-MM-dd HH:mm:ss.fffffffK", CultureInfo.InvariantCulture);
                    return Quote(iso);
                case DateTimeOffset dto:
                    var iso2 = dto.ToString("yyyy-MM-dd HH:mm:ss.fffffffK", CultureInfo.InvariantCulture);
                    return Quote(iso2);
                case Guid g:
                    return Quote(g.ToString());
                default:
                    // Fallback to string escape
                    return Quote(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
            }
        }

        private static string Quote(string s)
        {
            // Single-quote escape by doubling
            return "'" + s.Replace("'", "''", StringComparison.Ordinal) + "'";
        }

        private sealed record TableColumn(string Name, string Type);

        private static async Task<List<TableColumn>> GetTableColumnsAsync(SqliteConnection conn, string table, CancellationToken ct)
        {
            var cols = new List<TableColumn>(16);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info('{table.Replace("'", "''", StringComparison.Ordinal)}')";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var name = reader[1]?.ToString() ?? string.Empty; // name column
                var type = reader[2]?.ToString(); // type column
                if (!string.IsNullOrWhiteSpace(name))
                    cols.Add(new TableColumn(name, type ?? string.Empty));
            }
            return cols;
        }
    }
}
