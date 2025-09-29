using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForexExchange.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFrozenToHistoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "CustomerBalanceHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "CurrencyPoolHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFrozen",
                table: "BankAccountBalanceHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Iranian Toman");

            migrationBuilder.UpdateData(
                table: "CurrencyPools",
                keyColumn: "Id",
                keyValue: 1,
                column: "Notes",
                value: "Iranian Toman pool - initial setup");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "CustomerBalanceHistory");

            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "CurrencyPoolHistory");

            migrationBuilder.DropColumn(
                name: "IsFrozen",
                table: "BankAccountBalanceHistory");

            migrationBuilder.UpdateData(
                table: "Currencies",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "Iranian Rial");

            migrationBuilder.UpdateData(
                table: "CurrencyPools",
                keyColumn: "Id",
                keyValue: 1,
                column: "Notes",
                value: "Iranian Rial pool - initial setup");
        }
    }
}
