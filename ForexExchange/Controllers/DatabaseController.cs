using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using DNTPersianUtils.Core;
using ForexExchange.Services.Notifications;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Extensions;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DatabaseController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ICurrencyPoolService _currencyPoolService;
        private readonly ICentralFinancialService _centralFinancialService;
        private readonly INotificationHub _notificationHub;
        private readonly UserManager<ApplicationUser> _userManager;

        public DatabaseController(ForexDbContext context, IWebHostEnvironment environment,
            ICurrencyPoolService currencyPoolService, ICentralFinancialService centralFinancialService,
            INotificationHub notificationHub, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _environment = environment;
            _currencyPoolService = currencyPoolService;
            _centralFinancialService = centralFinancialService;
            _notificationHub = notificationHub;
            _userManager = userManager;
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
                    .Select(h => new
                    {
                        h.Id,
                        h.CurrencyCode,
                        h.TransactionType,
                        h.ReferenceId,
                        h.TransactionAmount,
                        h.Description,
                        h.TransactionDate
                    })
                    .ToListAsync();

                return Json(new
                {
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
        public async Task<IActionResult> CreateManualCustomerBalanceHistory(
            int customerId,
            string currencyCode,
            decimal amount,
            string reason,
            DateTime transactionDate)
        {
            try
            {
                // Validate inputs
                if (customerId <= 0)
                {
                    TempData["Error"] = "لطفاً مشتری معتبری انتخاب کنید";
                    return RedirectToAction("Index");
                }

                if (string.IsNullOrWhiteSpace(currencyCode))
                {
                    TempData["Error"] = "لطفاً ارز معتبری انتخاب کنید";
                    return RedirectToAction("Index");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["Error"] = "لطفاً دلیل تراکنش را وارد کنید";
                    return RedirectToAction("Index");
                }

                // Get customer name for display
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
                var customerName = customer?.FullName ?? $"مشتری {customerId}";

                // Get current user for notification exclusion
                var currentUser = await _userManager.GetUserAsync(User);

                // Create the manual history record with notification handling in service layer
                await _centralFinancialService.CreateManualCustomerBalanceHistoryAsync(
                    customerId: customerId,
                    currencyCode: currencyCode,
                    amount: amount,
                    reason: reason,
                    transactionDate: transactionDate,
                    performedBy: "Database Admin",
                    performingUserId: currentUser?.Id
                );

                var summary = new[]
                {
                    "✅ رکورد دستی تاریخچه موجودی ایجاد شد",
                    $"👤 مشتری: {customerName}",
                    $"💰 مبلغ: {amount:N2} {currencyCode}",
                    $"📅 تاریخ تراکنش: {transactionDate:yyyy-MM-dd}",
                    $"📝 دلیل: {reason}",
                    "",
                    "⚠️ مهم: برای اطمینان از انسجام موجودی‌ها، حتماً دکمه 'بازمحاسبه بر اساس تاریخ تراکنش' را اجرا کنید"
                };

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "تراکنش دستی با موفقیت ثبت شد" });
                }

                TempData["Success"] = string.Join("<br/>", summary);
            }
            catch (Exception ex)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = $"خطا در ایجاد رکورد دستی: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در ایجاد رکورد دستی: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteManualCustomerBalanceHistory(long transactionId)
        {
            try
            {
                // Find the manual transaction record
                var transaction = await _context.CustomerBalanceHistory
                    .Include(h => h.Customer)
                    .FirstOrDefaultAsync(h => h.Id == transactionId &&
                                           h.TransactionType == CustomerBalanceTransactionType.Manual);

                if (transaction == null)
                {
                    // Check if this is an AJAX request
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, error = "تراکنش دستی یافت نشد یا این تراکنش قابل حذف نیست" });
                    }

                    TempData["Error"] = "تراکنش دستی یافت نشد یا این تراکنش قابل حذف نیست";
                    return RedirectToAction("Index");
                }

                var customerName = transaction.Customer?.FullName ?? $"مشتری {transaction.CustomerId}";
                var amount = transaction.TransactionAmount;
                var currencyCode = transaction.CurrencyCode;

                // Get current user for notification exclusion
                var currentUser = await _userManager.GetUserAsync(User);

                // Delete the transaction and recalculate balances with notification handling in service layer
                await _centralFinancialService.DeleteManualCustomerBalanceHistoryAsync(transactionId, "Database Admin", currentUser?.Id);

                var summary = new[]
                {
                    "✅ تعدیل دستی با موفقیت حذف شد",
                    $"👤 مشتری: {customerName}",
                    $"💰 مبلغ حذف شده: {amount:N2} {currencyCode}",
                    "",
                    "🔄 موجودی‌ها بازمحاسبه شدند"
                };

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "تعدیل دستی با موفقیت حذف شد و موجودی‌ها بازمحاسبه شدند" });
                }

                TempData["Success"] = string.Join("<br/>", summary);
            }
            catch (Exception ex)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = $"خطا در حذف تعدیل دستی: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در حذف تعدیل دستی: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateNotesAndDescriptions()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var performedBy = user?.UserName ?? "Admin";
                var logMessages = new List<string>();

                logMessages.Add("=== UPDATING NOTES AND DESCRIPTIONS ===");
                logMessages.Add($"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                logMessages.Add($"Performed by: {performedBy}");
                logMessages.Add("");

                using var dbTransaction = await _context.Database.BeginTransactionAsync();

                // STEP 1: Update Order Notes
                logMessages.Add("STEP 1: Updating Order Notes...");
                var orders = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => !o.IsDeleted)
                    .ToListAsync();

                foreach (var order in orders)
                {
                    var note = $"معامله {order.CurrencyPair} - مشتری: {order.Customer?.FullName ?? "نامشخص"} - مقدار: {order.FromAmount:N0} {order.FromCurrency?.Code ?? ""} → {order.ToAmount:N0} {order.ToCurrency?.Code ?? ""} - نرخ: {order.Rate:N4}";
                    order.Notes = note;
                }

                var ordersUpdated = await _context.SaveChangesAsync();
                logMessages.Add($"✓ Updated {ordersUpdated} order notes");

                // STEP 2: Update AccountingDocument Notes
                logMessages.Add("");
                logMessages.Add("STEP 2: Updating AccountingDocument Notes...");
                var documents = await _context.AccountingDocuments
                    .Include(d => d.PayerCustomer)
                    .Include(d => d.ReceiverCustomer)
                    .Include(d => d.PayerBankAccount)
                    .Include(d => d.ReceiverBankAccount)
                    .Where(d => !d.IsDeleted)
                    .ToListAsync();

                foreach (var doc in documents)
                {
                    var note = $"{doc.Title} - مبلغ: {doc.Amount:N0} {doc.CurrencyCode} - از: {doc.PayerDisplayText} → به: {doc.ReceiverDisplayText}";
                    if (!string.IsNullOrEmpty(doc.Description))
                        note += $" - توضیحات: {doc.Description}";
                    doc.Notes = note;
                }

                var documentsUpdated = await _context.SaveChangesAsync();
                logMessages.Add($"✓ Updated {documentsUpdated} accounting document notes");

                // STEP 3: Update CustomerBalanceHistory Descriptions
                logMessages.Add("");
                logMessages.Add("STEP 3: Updating CustomerBalanceHistory Descriptions...");

                // Update descriptions for Order transactions
                var orderHistoryRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.Order && !h.IsDeleted)
                    .ToListAsync();

                foreach (var history in orderHistoryRecords)
                {
                    var order = orders.FirstOrDefault(o => o.Id == history.ReferenceId);
                    if (order != null)
                    {
                        // Description includes customer info (from order.Notes)
                        if (!string.IsNullOrEmpty(order.Notes))
                        {
                            history.Description = order.Notes;
                        }

                        // Note includes transaction details without customer info
                        var note = $"{order.CurrencyPair} - مقدار: {order.FromAmount:N0} {order.FromCurrency?.Code ?? ""} → {order.ToAmount:N0} {order.ToCurrency?.Code ?? ""} - نرخ: {order.Rate:N4}";
                        history.Note = note;
                    }
                }

                // Update descriptions for AccountingDocument transactions
                var documentHistoryRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.AccountingDocument && !h.IsDeleted)
                    .ToListAsync();

                foreach (var history in documentHistoryRecords)
                {
                    var document = documents.FirstOrDefault(d => d.Id == history.ReferenceId);
                    if (document != null)
                    {
                        // Description includes customer info (from document.Notes)
                        if (!string.IsNullOrEmpty(document.Notes))
                        {
                            history.Description = document.Notes;
                        }

                        // Note includes transaction details without customer info
                        var note = $"{document.Type.GetDisplayName()} - مبلغ: {document.Amount:N0} {document.CurrencyCode}";
                        if (!string.IsNullOrEmpty(document.ReferenceNumber))
                            note += $" - شناسه تراکنش: {document.ReferenceNumber}";

                        history.Note = note;
                    }
                }

                var historyUpdated = await _context.SaveChangesAsync();
                logMessages.Add($"✓ Updated {historyUpdated} customer balance history descriptions");

                await dbTransaction.CommitAsync();

                logMessages.Add("");
                logMessages.Add("=== UPDATE COMPLETED SUCCESSFULLY ===");
                logMessages.Add($"Total orders processed: {orders.Count}");
                logMessages.Add($"Total documents processed: {documents.Count}");
                logMessages.Add($"Total history records updated: {orderHistoryRecords.Count + documentHistoryRecords.Count}");

                TempData["Success"] = string.Join("<br/>", logMessages);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در بروزرسانی یادداشت‌ها: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // / <summary>
        // / Comprehensive rebuild of all financial balances based on new IsFrozen strategy:
        // / - Pool balances rebuilt from non-deleted AND non-frozen orders only with coherent history starting from zero
        // / - Bank account balances rebuilt from non-deleted AND non-frozen documents only with coherent history starting from zero
        // / - Customer balance history rebuilt from non-deleted orders, documents, and manual records (including frozen orders/documents)
        // / - Active buy/sell counts recalculated properly based on non-frozen orders
        // / 
        // / This ensures frozen historical records don't affect current balance calculations
        // / but are preserved for customer balance history audit trail, including manual adjustments.
        // / Creates coherent balance history chains starting from zero before first non-frozen record.
        // / </summary>
        [HttpPost]
        public async Task<IActionResult> RebuildAllFinancialBalances()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var performedBy = user?.UserName ?? "Admin";
                var logMessages = new List<string>
                {
                    "=== COMPREHENSIVE FINANCIAL BALANCE REBUILD WITH COHERENT HISTORY ===",
                    $"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"Performed by: {performedBy}",
                    ""
                };

                // Get all manual customer balance history records (including frozen, not deleted)
                var manualCustomerRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.Manual && !h.IsDeleted)
                    .ToListAsync();

                // Get all manual bank account balance history records (including frozen, not deleted)
                var manualBankAccountRecords = await _context.BankAccountBalanceHistory
                    .Where(h => h.TransactionType == BankAccountTransactionType.ManualEdit && !h.IsDeleted)
                    .ToListAsync();

                // Get all manual pool history records (including frozen, not deleted)
                var manualPoolRecords = await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionType == CurrencyPoolTransactionType.ManualEdit && !h.IsDeleted)
                    .ToListAsync();

                logMessages.Add($"All manual records saved in memory: Customer={manualCustomerRecords.Count}, BankAccount={manualBankAccountRecords.Count}, Pool={manualPoolRecords.Count}");



                using var dbTransaction = await _context.Database.BeginTransactionAsync();

                // STEP 1: Clear all history tables and reset balances to zero
                logMessages.Add("STEP 1: Clearing all history tables and resetting balances...");

                // Clear pool history (will be rebuilt)
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM CurrencyPoolHistory");

                // Clear bank account balance history (will be rebuilt)
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM BankAccountBalanceHistory");

                // Clear customer balance history (will be rebuilt)
                var deletedHistoryCount = await _context.Database.ExecuteSqlRawAsync("DELETE FROM CustomerBalanceHistory");
                var remainingManualCount = await _context.CustomerBalanceHistory.CountAsync(h => h.TransactionType == CustomerBalanceTransactionType.Manual);
                logMessages.Add($"✓ Cleared non-manual customer balance history, preserved {remainingManualCount} manual records");

                // Reset customer balances
                var customerBalances = await _context.CustomerBalances.ToListAsync();
                foreach (var balance in customerBalances)
                {
                    balance.Balance = 0;
                    balance.LastUpdated = DateTime.UtcNow;
                }

                // Reset currency pool balances and active counts
                var poolBalances = await _context.CurrencyPools.ToListAsync();
                foreach (var pool in poolBalances)
                {
                    pool.Balance = 0;
                    pool.ActiveBuyOrderCount = 0;
                    pool.ActiveSellOrderCount = 0;
                    pool.LastUpdated = DateTime.UtcNow;
                }

                // Reset bank account balances
                var bankBalances = await _context.BankAccountBalances.ToListAsync();
                foreach (var balance in bankBalances)
                {
                    balance.Balance = 0;
                    balance.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Cleared non-manual history tables and reset {customerBalances.Count} customer balances, {poolBalances.Count} pool balances, {bankBalances.Count} bank account balances to zero");



                // STEP 2: Create coherent pool history starting from zero for each currency
                logMessages.Add("");
                logMessages.Add("STEP 2: Creating coherent pool history with zero-starting balance chains...");

                var activeOrders = await _context.Orders
                    .Where(o => !o.IsDeleted && !o.IsFrozen)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .OrderBy(o => o.CreatedAt)
                    .ToListAsync();

                logMessages.Add($"Processing {activeOrders.Count} active (non-deleted, non-frozen) orders and {manualPoolRecords.Count} manual pool records...");

                // Create unified transaction items for pools from orders and manual records
                var poolTransactionItems = new List<(string CurrencyCode, DateTime TransactionDate, string TransactionType, int? ReferenceId, decimal Amount, string PoolTransactionType, string Description)>();

                // Add order transactions
                foreach (var o in activeOrders)
                {
                    var currentOrder = _context.Orders.Find(o.Id);
                    if (currentOrder is null) continue;
                    // Institution receives FromAmount in FromCurrency (pool increases)
                    poolTransactionItems.Add((o.FromCurrency.Code, o.CreatedAt, "Order", o.Id, o.FromAmount, "Buy", currentOrder?.Notes ?? ""));

                    // Institution pays ToAmount in ToCurrency (pool decreases)
                    poolTransactionItems.Add((o.ToCurrency.Code, o.CreatedAt, "Order", o.Id, -o.ToAmount, "Sell", currentOrder?.Notes ?? ""));
                }

                // Add manual pool records as transactions
                foreach (var manual in manualPoolRecords)
                {
                    poolTransactionItems.Add((
                        manual.CurrencyCode,
                        manual.TransactionDate,
                        "Manual",
                        (int?)manual.Id,
                        manual.TransactionAmount,
                        "Manual",
                        manual.Description ?? "Manual adjustment"
                    ));
                }

                // Group by currency code to create coherent history per currency
                var currencyGroups = poolTransactionItems
                    .GroupBy(x => x.CurrencyCode)
                    .ToList();

                foreach (var currencyGroup in currencyGroups)
                {
                    var currencyCode = currencyGroup.Key;
                    var currencyTransactions = currencyGroup.OrderBy(x => x.TransactionDate).ToList();

                    if (!currencyTransactions.Any()) continue;

                    // Find the earliest transaction for this currency
                    var firstTransaction = currencyTransactions.First();
                    var zeroDateTime = firstTransaction.TransactionDate.AddMinutes(-1);

                    // Create zero-starting pool history record
                    var zeroPoolHistory = new CurrencyPoolHistory
                    {
                        CurrencyCode = currencyCode,
                        TransactionType = CurrencyPoolTransactionType.ManualEdit,
                        ReferenceId = null,
                        BalanceBefore = 0,
                        TransactionAmount = 0,
                        BalanceAfter = 0,
                        Description = "Zero-start balance for coherent history chain (non-frozen records only)",
                        TransactionDate = zeroDateTime,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = performedBy,
                        IsDeleted = false
                    };
                    _context.CurrencyPoolHistory.Add(zeroPoolHistory);

                    // Process transactions chronologically for this currency
                    decimal runningBalance = 0;
                    int buyCount = 0, sellCount = 0;
                    decimal totalBought = 0, totalSold = 0;

                    foreach (var transaction in currencyTransactions)
                    {
                        var transactionType = transaction.TransactionType switch
                        {
                            "Order" => CurrencyPoolTransactionType.Order,
                            "Manual" => CurrencyPoolTransactionType.ManualEdit,
                            _ => CurrencyPoolTransactionType.Order
                        };

                        var poolHistory = new CurrencyPoolHistory
                        {
                            CurrencyCode = currencyCode,
                            TransactionType = transactionType,
                            ReferenceId = transaction.ReferenceId,
                            BalanceBefore = runningBalance,
                            TransactionAmount = transaction.Amount,
                            BalanceAfter = runningBalance + transaction.Amount,
                            PoolTransactionType = transaction.PoolTransactionType,
                            Description = transaction.Description,
                            TransactionDate = transaction.TransactionDate,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = performedBy,
                            IsDeleted = false
                        };
                        _context.CurrencyPoolHistory.Add(poolHistory);

                        runningBalance = poolHistory.BalanceAfter;

                        // Update counts and totals for orders only (not manual records)
                        if (transaction.TransactionType == "Order")
                        {
                            if (transaction.PoolTransactionType == "Buy")
                            {
                                buyCount++;
                                totalBought += transaction.Amount;
                            }
                            else if (transaction.PoolTransactionType == "Sell")
                            {
                                sellCount++;
                                totalSold += Math.Abs(transaction.Amount);
                            }
                        }
                    }

                    // Update pool balance, active counts, and totals
                    var pool = await _context.CurrencyPools.FirstOrDefaultAsync(p => p.CurrencyCode == currencyCode);
                    if (pool != null)
                    {
                        pool.Balance = runningBalance;
                        pool.ActiveBuyOrderCount = buyCount;
                        pool.ActiveSellOrderCount = sellCount;
                        pool.TotalBought = totalBought;
                        pool.TotalSold = totalSold;
                        pool.LastUpdated = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Created coherent pool history for {currencyGroups.Count} currencies with {activeOrders.Count} active orders");

                // STEP 3: Create coherent bank account balance history starting from zero
                logMessages.Add("");
                logMessages.Add("STEP 3: Creating coherent bank account balance history with zero-starting balance chains...");

                var activeDocuments = await _context.AccountingDocuments
                    .Where(d => !d.IsDeleted && !d.IsFrozen)
                    .OrderBy(d => d.DocumentDate)
                    .ToListAsync();

                logMessages.Add($"Processing {activeDocuments.Count} active (non-deleted, non-frozen) documents and {manualBankAccountRecords.Count} manual bank account records...");

                // Create unified transaction items for bank accounts from documents and manual records
                var bankAccountTransactionItems = new List<(int BankAccountId, string CurrencyCode, DateTime TransactionDate, string TransactionType, int? ReferenceId, decimal Amount, string Description)>();

                // Add document transactions
                foreach (var d in activeDocuments)
                {

                    var currentDocument = _context.AccountingDocuments.Find(d.Id);
                    if (currentDocument is null) continue;
                    if (d.PayerType == PayerType.System && d.PayerBankAccountId.HasValue)
                        bankAccountTransactionItems.Add((d.PayerBankAccountId.Value, d.CurrencyCode, d.DocumentDate, "Document", d.Id, d.Amount, currentDocument?.Notes ?? string.Empty));
                    if (d.ReceiverType == ReceiverType.System && d.ReceiverBankAccountId.HasValue)
                        bankAccountTransactionItems.Add((d.ReceiverBankAccountId.Value, d.CurrencyCode, d.DocumentDate, "Document", d.Id, d.Amount, currentDocument?.Notes ?? string.Empty));
                }

                // Add manual bank account records as transactions
                foreach (var manual in manualBankAccountRecords)
                {
                    bankAccountTransactionItems.Add((
                        manual.BankAccountId,
                        "N/A", // Bank accounts don't have currency codes in the same way
                        manual.TransactionDate,
                        "Manual",
                        (int?)manual.Id,
                        manual.TransactionAmount,
                        manual.Description ?? "Manual adjustment"
                    ));
                }

                // Group by bank account to create coherent history
                var bankAccountGroups = bankAccountTransactionItems
                    .GroupBy(x => x.BankAccountId)
                    .ToList();

                foreach (var bankGroup in bankAccountGroups)
                {
                    var bankAccountId = bankGroup.Key;
                    var bankTransactions = bankGroup.OrderBy(x => x.TransactionDate).ToList();

                    if (!bankTransactions.Any()) continue;

                    // Find the earliest transaction for this bank account
                    var firstTransaction = bankTransactions.First();
                    var zeroDateTime = firstTransaction.TransactionDate.AddMinutes(-1);

                    // Create zero-starting bank account balance history record
                    var zeroBankHistory = new BankAccountBalanceHistory
                    {
                        BankAccountId = bankAccountId,
                        TransactionType = BankAccountTransactionType.ManualEdit,
                        ReferenceId = null,
                        BalanceBefore = 0,
                        TransactionAmount = 0,
                        BalanceAfter = 0,
                        Description = "Zero-start balance for coherent history chain (non-frozen records only)",
                        TransactionDate = zeroDateTime,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = performedBy,
                        IsDeleted = false
                    };
                    _context.BankAccountBalanceHistory.Add(zeroBankHistory);

                    // Process transactions chronologically for this bank account
                    decimal runningBalance = 0;

                    foreach (var transaction in bankTransactions)
                    {
                        var transactionType = transaction.TransactionType switch
                        {
                            "Document" => BankAccountTransactionType.Document,
                            "Manual" => BankAccountTransactionType.ManualEdit,
                            _ => BankAccountTransactionType.Document
                        };

                        var bankHistory = new BankAccountBalanceHistory
                        {
                            BankAccountId = bankAccountId,
                            TransactionType = transactionType,
                            ReferenceId = transaction.ReferenceId,
                            BalanceBefore = runningBalance,
                            TransactionAmount = transaction.Amount,
                            BalanceAfter = runningBalance + transaction.Amount,
                            Description = transaction.Description,
                            TransactionDate = transaction.TransactionDate,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = performedBy,
                            IsDeleted = false
                        };
                        _context.BankAccountBalanceHistory.Add(bankHistory);

                        runningBalance = bankHistory.BalanceAfter;
                    }

                    // Update bank account balance  
                    var finalBankAccountId = bankAccountId;
                    var bankBalance = await _context.BankAccountBalances
                        .FirstOrDefaultAsync(b => b.BankAccountId == finalBankAccountId);
                    if (bankBalance != null)
                    {
                        bankBalance.Balance = runningBalance;
                        bankBalance.LastUpdated = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Created coherent bank account balance history for {bankAccountGroups.Count} bank account + currency combinations");

                // STEP 4: Rebuild coherent customer balance history from orders, documents, and manual records (including frozen, excluding only deleted)
                logMessages.Add("");
                logMessages.Add("STEP 4: Rebuilding coherent customer balance history from orders, documents, and manual records (including frozen for customer history)...");

                // Get all non-deleted orders and documents
                var allValidDocuments = await _context.AccountingDocuments
                    .Where(d => !d.IsDeleted && d.IsVerified) // Include frozen documents for customer balance history
                    .ToListAsync();

                var allValidOrders = await _context.Orders
                    .Where(o => !o.IsDeleted) // Include frozen orders for customer balance history  
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .ToListAsync();



                logMessages.Add($"Processing {allValidDocuments.Count} valid documents, {allValidOrders.Count} valid orders, and {manualCustomerRecords.Count} manual customer records for customer balance history...");

                // Create unified transaction items for customers from orders, documents, and manual records
                var customerTransactionItems = new List<(int CustomerId, string CurrencyCode, DateTime TransactionDate, string TransactionType, string transactionCode, int? ReferenceId, decimal Amount, string Description)>();

                // Add document transactions
                foreach (var d in allValidDocuments)
                {
                    if (d.PayerType == PayerType.Customer && d.PayerCustomerId.HasValue)
                        customerTransactionItems.Add((d.PayerCustomerId.Value, d.CurrencyCode, d.DocumentDate, "Document", d.ReferenceNumber ?? string.Empty, d.Id, d.Amount, d.Description ?? string.Empty));
                    if (d.ReceiverType == ReceiverType.Customer && d.ReceiverCustomerId.HasValue)
                        customerTransactionItems.Add((d.ReceiverCustomerId.Value, d.CurrencyCode, d.DocumentDate, "Document", d.ReferenceNumber ?? string.Empty, d.Id, -d.Amount, d.Description ?? string.Empty));
                }

                // Add order transactions
                foreach (var o in allValidOrders)
                {
                    // Customer pays FromAmount in FromCurrency
                    customerTransactionItems.Add((o.CustomerId, o.FromCurrency.Code, o.CreatedAt, "Order", string.Empty, o.Id, -o.FromAmount, o.Notes ?? string.Empty));

                    // Customer receives ToAmount in ToCurrency
                    customerTransactionItems.Add((o.CustomerId, o.ToCurrency.Code, o.CreatedAt, "Order", string.Empty, o.Id, o.ToAmount, o.Notes ?? string.Empty));
                }

                logMessages.Add($"start adding  [{manualCustomerRecords.Count}] manual customer records");
                logMessages.Add($"customerTransactionItems is [{customerTransactionItems.Count}]");



                // Add manual customer records as transactions
                foreach (var manual in manualCustomerRecords)
                {
                    customerTransactionItems.Add((
                        manual.CustomerId,
                        manual.CurrencyCode,
                        manual.TransactionDate,
                        "Manual",
                        string.Empty,
                        (int?)manual.Id,
                        manual.TransactionAmount,
                        manual.Description ?? "Manual adjustment"
                    ));
                }
                logMessages.Add($"customerTransactionItems is [{customerTransactionItems.Count}]");


                // Group by customer + currency and create coherent history
                var customerGroups = customerTransactionItems
                    .GroupBy(x => new { x.CustomerId, x.CurrencyCode })
                    .ToList();

                logMessages.Add($"Creating coherent history for {customerGroups.Count} customer + currency combinations...");

                foreach (var customerGroup in customerGroups)
                {
                    var customerId = customerGroup.Key.CustomerId;
                    var currencyCode = customerGroup.Key.CurrencyCode;

                    // Order all transactions chronologically by TransactionDate
                    var orderedTransactions = customerGroup.OrderBy(x => x.TransactionDate).ToList();

                    if (!orderedTransactions.Any()) continue;

                    // Process transactions chronologically for this customer + currency
                    decimal runningBalance = 0;

                    foreach (var transaction in orderedTransactions)
                    {
                        var transactionType = transaction.TransactionType switch
                        {
                            "Order" => CustomerBalanceTransactionType.Order,
                            "Document" => CustomerBalanceTransactionType.AccountingDocument,
                            "Manual" => CustomerBalanceTransactionType.Manual,
                            _ => CustomerBalanceTransactionType.AccountingDocument
                        };
                        var note = $"{transactionType} - مبلغ: {transaction.Amount:N0} {transaction.CurrencyCode}";
                        if (!string.IsNullOrEmpty(transaction.transactionCode))
                            note += $" - شناسه تراکنش: {transaction.transactionCode}";

                        var customerHistory = new CustomerBalanceHistory
                        {
                            CustomerId = customerId,
                            CurrencyCode = currencyCode,
                            TransactionType = transactionType,
                            ReferenceId = transaction.ReferenceId,
                            BalanceBefore = runningBalance,
                            TransactionAmount = transaction.Amount,
                            BalanceAfter = runningBalance + transaction.Amount,
                            Description = transaction.Description,
                            TransactionNumber = transaction.transactionCode,
                            Note = note,
                            TransactionDate = transaction.TransactionDate,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = performedBy,
                            IsDeleted = false
                        };
                        _context.CustomerBalanceHistory.Add(customerHistory);

                        runningBalance = customerHistory.BalanceAfter;
                    }

                    // Update customer balance
                    var finalCustomerId = customerId;
                    var finalCustomerCurrencyCode = currencyCode;
                    var customerBalance = await _context.CustomerBalances
                        .FirstOrDefaultAsync(b => b.CustomerId == finalCustomerId && b.CurrencyCode == finalCustomerCurrencyCode);
                    if (customerBalance == null)
                    {
                        customerBalance = new CustomerBalance
                        {
                            CustomerId = customerId,
                            CurrencyCode = currencyCode,
                            Balance = 0,
                            LastUpdated = DateTime.UtcNow
                        };
                        _context.CustomerBalances.Add(customerBalance);
                    }
                    customerBalance.Balance = runningBalance;
                    customerBalance.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Rebuilt coherent customer balance history for {customerGroups.Count} customer + currency combinations from {allValidDocuments.Count} documents and {allValidOrders.Count} orders (manual records were preserved)");

                await dbTransaction.CommitAsync();

                logMessages.Add("");
                logMessages.Add("=== REBUILD COMPLETED SUCCESSFULLY ===");
                logMessages.Add($"Finished at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                logMessages.Add("✅ All balance histories now start from zero with coherent balance chains");
                logMessages.Add("✅ Active buy/sell counts recalculated based on non-frozen orders only");
                logMessages.Add("✅ Frozen records excluded from pool/bank calculations but included in customer history");
                logMessages.Add("✅ Manual customer balance adjustments preserved in complete customer history");

                var logSummary = string.Join("\n", logMessages);
                TempData["Success"] = "بازسازی کامل موجودی‌های مالی با زنجیره‌های منسجم موجودی با موفقیت انجام شد!";
                TempData["RecalcLog"] = logSummary;

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در بازسازی موجودی‌های مالی: {ex.Message}";
                return RedirectToAction("Index");
            }
        }



    }
}
