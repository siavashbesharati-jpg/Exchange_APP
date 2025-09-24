# PowerShell Script to Insert Manual Customer Balance History Records
# This script uses Entity Framework Core to insert records while ignoring foreign key constraints
# Date: 2025-09-24

param(
    [string]$ConnectionString = "Data Source=ForexExchange.db"
)

Write-Host "Starting insertion of manual balance history records..." -ForegroundColor Green

try {
    # Change to the project directory
    Set-Location "E:\IRANEXPEDIA\Exchange_APP\ForexExchange"
    
    # Create a temporary C# console application to insert records
    $tempScript = @"
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using System;
using System.Threading.Tasks;

var options = new DbContextOptionsBuilder<ForexDbContext>()
    .UseSqlite("$ConnectionString")
    .Options;

using var context = new ForexDbContext(options);

// Manual balance history records to insert
var records = new[]
{
    new CustomerBalanceHistory { Id = 1143, CustomerId = 38, CurrencyCode = "IRR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = -5000, TransactionAmount = 5000, BalanceAfter = 0, Description = "جهت تراز کردن بالانس", TransactionDate = DateTime.Parse("2025-09-21 13:42:00"), CreatedAt = DateTime.Parse("2025-09-21 10:09:39.0183089"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 1068, CustomerId = 5, CurrencyCode = "IRR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 549341500, TransactionAmount = -1326000, BalanceAfter = 548015500, Description = "ضرر آقای خداداد", TransactionDate = DateTime.Parse("2025-09-17 23:02:00"), CreatedAt = DateTime.Parse("2025-09-20 19:33:59.8319273"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 1013, CustomerId = 12, CurrencyCode = "IRR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = 41000000, BalanceAfter = 41000000, Description = "تعدیل دستی", TransactionDate = DateTime.Parse("2025-08-31 21:06:00"), CreatedAt = DateTime.Parse("2025-09-20 17:36:40.3508394"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 1010, CustomerId = 20, CurrencyCode = "OMR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = 440, BalanceAfter = 440, Description = "تعدیل دستی", TransactionDate = DateTime.Parse("2025-08-31 21:03:00"), CreatedAt = DateTime.Parse("2025-09-20 17:33:39.6532784"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 1006, CustomerId = 12, CurrencyCode = "OMR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = 41, BalanceAfter = 41, Description = "تعدیل دستی", TransactionDate = DateTime.Parse("2025-08-31 20:52:00"), CreatedAt = DateTime.Parse("2025-09-20 17:23:26.1773133"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 943, CustomerId = 8, CurrencyCode = "IRR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = -42715000, BalanceAfter = -42715000, Description = "تعدیل دستی", TransactionDate = DateTime.Parse("2025-08-31 18:23:00"), CreatedAt = DateTime.Parse("2025-09-20 14:54:05.0714444"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 936, CustomerId = 25, CurrencyCode = "IRR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = -847131000, BalanceAfter = -847131000, Description = "تعدیل دستی", TransactionDate = DateTime.Parse("2025-08-31 18:01:00"), CreatedAt = DateTime.Parse("2025-09-20 14:32:31.5386377"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 929, CustomerId = 3, CurrencyCode = "OMR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = -2899.78m, BalanceAfter = -2899.78m, Description = "تعدیل دستی", TransactionDate = DateTime.Parse("2025-08-31 17:52:00"), CreatedAt = DateTime.Parse("2025-09-20 14:23:18.2009196"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 901, CustomerId = 24, CurrencyCode = "OMR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = -5, BalanceAfter = -5, Description = "تعدیل دستی", TransactionDate = DateTime.Parse("2025-08-31 17:06:00"), CreatedAt = DateTime.Parse("2025-09-20 13:36:41.4347372"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 813, CustomerId = 31, CurrencyCode = "OMR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = 137.18m, BalanceAfter = 137.18m, Description = "تعدیل دستی", TransactionDate = DateTime.Parse("2025-08-31 11:55:00"), CreatedAt = DateTime.Parse("2025-09-20 08:25:24.7126785"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 800, CustomerId = 31, CurrencyCode = "AED", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = 100000, BalanceAfter = 100000, Description = "تعدیل دستی", TransactionDate = DateTime.Parse("2025-08-31 09:57:00"), CreatedAt = DateTime.Parse("2025-09-20 06:25:08.7955723"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 777, CustomerId = 5, CurrencyCode = "IRR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = 400000, BalanceAfter = 400000, Description = "تعدیل دستی", TransactionDate = DateTime.Parse("2025-08-31 20:25:00"), CreatedAt = DateTime.Parse("2025-09-19 16:55:49.0802431"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 592, CustomerId = 4, CurrencyCode = "OMR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = 10677.13m, BalanceAfter = 10677.13m, Description = "بالانس اولیه در 31 آگست", TransactionDate = DateTime.Parse("2025-08-31 23:20:00"), CreatedAt = DateTime.Parse("2025-09-17 19:47:43.0749174"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 654, CustomerId = 5, CurrencyCode = "OMR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = 60.67m, BalanceAfter = 60.67m, Description = "تعدیل دستی", TransactionDate = DateTime.Parse("2025-08-31 22:38:00"), CreatedAt = DateTime.Parse("2025-09-18 19:05:34.8473923"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 656, CustomerId = 30, CurrencyCode = "OMR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 3194.3m, TransactionAmount = -4.5m, BalanceAfter = 3189.8m, Description = "تعدیل دستی", TransactionDate = DateTime.Parse("2025-08-31 22:41:00"), CreatedAt = DateTime.Parse("2025-09-18 19:08:43.176507"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 595, CustomerId = 35, CurrencyCode = "IRR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = 31000000, BalanceAfter = 31000000, Description = "بالانس اولیه در 31 آگست", TransactionDate = DateTime.Parse("2025-08-31 20:52:00"), CreatedAt = DateTime.Parse("2025-09-18 17:20:50.0423856"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 591, CustomerId = 4, CurrencyCode = "AED", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = 3740, BalanceAfter = 3740, Description = "بالانس اولیه در تاریخ 31 آگست", TransactionDate = DateTime.Parse("2025-08-31 23:19:00"), CreatedAt = DateTime.Parse("2025-09-17 19:46:57.5262316"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 461, CustomerId = 30, CurrencyCode = "OMR", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = 3194.3m, BalanceAfter = 3194.3m, Description = "بالانس اولیه 31 آگست", TransactionDate = DateTime.Parse("2025-08-31 21:31:00"), CreatedAt = DateTime.Parse("2025-09-16 17:59:19.9557307"), CreatedBy = "Database Admin", IsDeleted = false },
    new CustomerBalanceHistory { Id = 448, CustomerId = 32, CurrencyCode = "AED", TransactionType = CustomerBalanceTransactionType.Manual, ReferenceId = null, BalanceBefore = 0, TransactionAmount = 550, BalanceAfter = 550, Description = "بالانس اولیه در تاریخ 31 آگست", TransactionDate = DateTime.Parse("2025-08-31 12:55:00"), CreatedAt = DateTime.Parse("2025-09-16 09:22:40.3551559"), CreatedBy = "Database Admin", IsDeleted = false }
};

Console.WriteLine("Inserting manual balance history records...");

// Use ExecuteSqlRaw to bypass foreign key constraints
await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

try 
{
    foreach (var record in records)
    {
        // Check if record already exists
        var exists = await context.CustomerBalanceHistory.AnyAsync(h => h.Id == record.Id);
        if (!exists)
        {
            context.CustomerBalanceHistory.Add(record);
            Console.WriteLine($"Adding record ID {record.Id} for Customer {record.CustomerId} ({record.CurrencyCode})");
        }
        else
        {
            Console.WriteLine($"Record ID {record.Id} already exists, skipping...");
        }
    }
    
    await context.SaveChangesAsync();
    Console.WriteLine("Records inserted successfully!");
    
    // Verify insertions
    var insertedCount = await context.CustomerBalanceHistory
        .CountAsync(h => h.TransactionType == CustomerBalanceTransactionType.Manual);
    Console.WriteLine($"Total manual records in database: {insertedCount}");
}
finally 
{
    await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
}
"@

    # Write the script to a temporary file
    $tempFile = "temp_insert_script.cs"
    $tempScript | Out-File -FilePath $tempFile -Encoding UTF8
    
    Write-Host "Executing insertion script..." -ForegroundColor Yellow
    
    # Run the script using dotnet script or compile and run
    dotnet run --project . -- --script $tempFile
    
    # Clean up
    if (Test-Path $tempFile) {
        Remove-Item $tempFile
    }
    
    Write-Host "Manual balance history records insertion completed!" -ForegroundColor Green
}
catch {
    Write-Error "Error inserting records: $($_.Exception.Message)"
    exit 1
}