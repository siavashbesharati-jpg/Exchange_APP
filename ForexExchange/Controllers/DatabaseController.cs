using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using DNTPersianUtils.Core;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DatabaseController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ICurrencyPoolService _currencyPoolService;
        private readonly ICentralFinancialService _centralFinancialService;

        public DatabaseController(ForexDbContext context, IWebHostEnvironment environment, 
            ICurrencyPoolService currencyPoolService, ICentralFinancialService centralFinancialService)
        {
            _context = context;
            _environment = environment;
            _currencyPoolService = currencyPoolService;
            _centralFinancialService = centralFinancialService;
        }

        public IActionResult Index()
        {
            var model = new DatabaseManagementViewModel
            {
                CustomersCount = _context.Customers.Where(c => !c.IsSystem).Count(),
                OrdersCount = _context.Orders.Count(),
                CurrencyPoolsCount = _context.CurrencyPools.Count(),
                // TODO: Replace with AccountingDocument counts
                TransactionsCount = 0, // _context.Transactions.Count(),
                ExchangeRatesCount = _context.ExchangeRates.Count(),
                AccountingDocumentsCount = 0 // _context.Receipts.Count()
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> FixPoolHistoryReferenceIds()
        {
            try
            {
                // Find CurrencyPoolHistory records with TransactionType = Order but ReferenceId = null
                var brokenRecords = await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionType == CurrencyPoolTransactionType.Order && h.ReferenceId == null)
                    .ToListAsync();

                if (!brokenRecords.Any())
                {
                    return Json(new { success = true, message = "All Order records already have ReferenceId set", fixedCount = 0 });
                }

                // Try to match them with orders based on description
                int fixedCount = 0;
                foreach (var record in brokenRecords)
                {
                    // Extract order ID from description like "Bought from customer via Order 123"
                    if (!string.IsNullOrEmpty(record.Description))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(record.Description, @"Order (\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int orderId))
                        {
                            // Verify the order exists
                            var orderExists = await _context.Orders.AnyAsync(o => o.Id == orderId);
                            if (orderExists)
                            {
                                record.ReferenceId = orderId;
                                fixedCount++;
                            }
                        }
                    }
                }

                if (fixedCount > 0)
                {
                    await _context.SaveChangesAsync();
                }

                return Json(new { 
                    success = true, 
                    message = $"Fixed {fixedCount} CurrencyPoolHistory records",
                    totalBrokenRecords = brokenRecords.Count,
                    fixedCount = fixedCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CreateBackup()
        {
            try
            {
                var now = DateTime.Now;
                var backupFileName = $"Taban_Backup_{now.GetPersianYear()}-{now.GetPersianMonth()}-{now.GetPersianDayOfMonth()}-{now.Hour}-{now.Minute}.tbn";
                var backupPath = Path.Combine(_environment.WebRootPath, "backups");

                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                }

                var fullBackupPath = Path.Combine(backupPath, backupFileName);

                // Create SQLite backup by copying the database file
                var connectionString = _context.Database.GetConnectionString();
                var dbPath = connectionString?.Replace("Data Source=", "").Replace(";", "");

                if (!string.IsNullOrEmpty(dbPath) && System.IO.File.Exists(dbPath))
                {
                    System.IO.File.Copy(dbPath, fullBackupPath, true);
                }

                TempData["Success"] = $"پشتیبان‌گیری با موفقیت ایجاد شد: {backupFileName}";

                // Return the file directly for download
                var fileBytes = System.IO.File.ReadAllBytes(fullBackupPath);
                return File(fileBytes, "application/octet-stream", backupFileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در ایجاد پشتیبان: {ex.Message}";
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> TestPoolHistory()
        {
            try
            {
                var historyCount = await _context.CurrencyPoolHistory.CountAsync();
                var orderTransactions = await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionType == CurrencyPoolTransactionType.Order)
                    .Take(10)
                    .Select(h => new {
                        h.Id,
                        h.CurrencyCode,
                        h.TransactionType,
                        h.ReferenceId,
                        h.TransactionAmount,
                        h.Description,
                        h.TransactionDate
                    })
                    .ToListAsync();

                return Json(new { 
                    totalHistoryRecords = historyCount,
                    orderTransactions = orderTransactions,
                    orderCount = orderTransactions.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult DownloadBackup(string fileName)
        {
            try
            {
                var backupPath = Path.Combine(_environment.WebRootPath, "backups", fileName);

                if (!System.IO.File.Exists(backupPath))
                {
                    TempData["Error"] = "فایل پشتیبان یافت نشد";
                    return RedirectToAction("Index");
                }

                var fileBytes = System.IO.File.ReadAllBytes(backupPath);
                return File(fileBytes, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در دانلود فایل: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

      

        

        [HttpPost]
        public async Task<IActionResult> RestoreDatabase(IFormFile backupFile)
        {
            if (backupFile == null || backupFile.Length == 0)
            {
                TempData["Error"] = "لطفاً فایل پشتیبان را انتخاب کنید";
                return RedirectToAction("Index");
            }

            try
            {
                // Create automatic backup before restore (file copy)
                var now = DateTime.Now;
                var backupFileName = $"-Auto-Taban_Backup_{now.GetPersianYear()}-{now.GetPersianMonth()}-{now.GetPersianDayOfMonth()}-{now.Hour}-{now.Minute}.tbn";
                var backupPath = Path.Combine(_environment.WebRootPath, "backups");
                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                }

                var autoBackupPath = Path.Combine(backupPath, backupFileName);
                var connectionString = _context.Database.GetConnectionString();
                var dbPath = connectionString?.Replace("Data Source=", "").Replace(";", "");

                if (!string.IsNullOrEmpty(dbPath) && System.IO.File.Exists(dbPath))
                {
                    System.IO.File.Copy(dbPath, autoBackupPath, true);
                }

                // Save uploaded file temporarily
                var tempPath = Path.GetTempFileName();
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await backupFile.CopyToAsync(stream);
                }

                // Use SQLite backup API to copy contents from uploaded DB into the live database
                // This avoids deleting/replacing the file while it's in use
                var busyDest = $"Data Source={dbPath};Cache=Shared";
                var busySrc = $"Data Source={tempPath};Mode=ReadOnly;Cache=Shared";

                using (var dest = new Microsoft.Data.Sqlite.SqliteConnection(busyDest))
                using (var src = new Microsoft.Data.Sqlite.SqliteConnection(busySrc))
                {
                    await dest.OpenAsync();
                    await src.OpenAsync();

                    // Set busy timeout via PRAGMA on both connections (connection string keyword not supported)
                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA busy_timeout=5000;";
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = src.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA busy_timeout=5000;";
                        cmd.ExecuteNonQuery();
                    }

                    // Ensure WAL is checkpointed to reduce locks
                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
                        cmd.ExecuteNonQuery();
                    }
                    // Temporarily disable foreign keys during restore to avoid temporary constraint errors
                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA foreign_keys=OFF;";
                        cmd.ExecuteNonQuery();
                    }

                    // Copy database content
                    src.BackupDatabase(dest);

                    using (var cmd = dest.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA foreign_keys=ON;";
                        cmd.ExecuteNonQuery();
                    }
                }

                // Cleanup temp file with a few retries; ignore failures
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            System.IO.File.Delete(tempPath);
                            break;
                        }
                        catch (IOException)
                        {
                            await Task.Delay(200);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            await Task.Delay(200);
                        }
                    }
                }
                catch { /* ignore cleanup issues */ }

                _context.ChangeTracker.Clear();

                TempData["Success"] = $"بازیابی پایگاه داده با موفقیت انجام شد. پشتیبان خودکار ایجاد شد: {backupFileName}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در بازیابی پایگاه داده: {ex.Message}";
            }

            return RedirectToAction("Index");
        }



        [HttpPost]
        public async Task<IActionResult> CleanCustomers()
        {
            try
            {
                // Delete in proper order to avoid foreign key constraint errors

                // 1. First remove ApplicationUser accounts (except Admin/Manager accounts)
                var usersToDelete = _context.Users
                    .Include(u => u.Customer)
                    .Where(u => u.Customer != null && u.Customer.IsSystem == false)
                    .ToList();
                _context.Users.RemoveRange(usersToDelete);

                // TODO: Remove AccountingDocuments and CustomerBalances in new architecture
                /*
                // 2. Remove Receipts (they reference Orders and Customers)
                _context.Receipts.RemoveRange(_context.Receipts);

                // 3. Remove Transactions (they reference Orders and Customers)
                _context.Transactions.RemoveRange(_context.Transactions);
                */

                // 4. Remove Orders (they reference Customers)
                _context.Orders.RemoveRange(_context.Orders);

                // 5. Finally remove non-system Customers
                var customersToDelete = _context.Customers.Where(c => c.IsSystem == false).ToList();
                _context.Customers.RemoveRange(customersToDelete);

                await _context.SaveChangesAsync();

                TempData["Success"] = $"تمام مشتریان، سفارشات، تراکنش‌ها، رسیدها و حساب‌های کاربری مرتبط پاک شدند. مشتریان سیستمی حفظ شدند.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در پاکسازی مشتریان: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CleanOrders()
        {
            try
            {
                // TODO: Remove accounting documents first, then orders in new architecture
                /*
                // Remove receipts first, then orders
                _context.Receipts.RemoveRange(_context.Receipts);
                */
                _context.Orders.RemoveRange(_context.Orders);

                await _context.SaveChangesAsync();
                TempData["Success"] = "تمام سفارشات پاک شدند";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در پاکسازی سفارشات: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> CleanPools()
        {
            try
            {
                await _currencyPoolService.CleanPoolAsync();
                TempData["Success"] = "تمام صندوق های ارز پاک شدند";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در پاکسازی صندوق ها: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> RecalculateIRRPool()
        {
            try
            {
                // First, let's check the current status
                var irrPool = await _context.CurrencyPools
                    .FirstOrDefaultAsync(cp => cp.CurrencyCode == "IRR");
                
                var irrOrders = await _context.Orders
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => o.FromCurrency.Code == "IRR" || o.ToCurrency.Code == "IRR")
                    .ToListAsync();

                // Calculate expected IRR pool adjustment
                decimal expectedAdjustment = 0;
                foreach (var order in irrOrders)
                {
                    if (order.FromCurrency.Code == "IRR")
                        expectedAdjustment += order.FromAmount;
                    if (order.ToCurrency.Code == "IRR")
                        expectedAdjustment -= order.ToAmount;
                }

                TempData["Info"] = $"Before: IRR Pool exists: {irrPool != null}, Balance: {irrPool?.Balance ?? 0}, IRR Orders: {irrOrders.Count}, Expected adjustment: {expectedAdjustment}";

                await _centralFinancialService.RecalculateIRRPoolFromOrdersAsync();
                
                // Check after recalculation
                var irrPoolAfter = await _context.CurrencyPools
                    .FirstOrDefaultAsync(cp => cp.CurrencyCode == "IRR");
                    
                TempData["Success"] = $"موجودی صندوق IRR با موفقیت بازمحاسبه شد. موجودی نهایی: {irrPoolAfter?.Balance ?? 0}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در بازمحاسبه صندوق IRR: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}
