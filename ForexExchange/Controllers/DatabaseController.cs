using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using ForexExchange.Scripts;
using DNTPersianUtils.Core;
using ForexExchange.Services.Notifications;
using Microsoft.AspNetCore.Identity;

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

                return Json(new
                {
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

                TempData["Success"] = $"تمام مشتریان، معاملات ، تراکنش‌ها، رسیدها و حساب‌های کاربری مرتبط پاک شدند. مشتریان سیستمی حفظ شدند.";
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
                TempData["Success"] = "تمام معاملات  پاک شدند";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در پاکسازی معاملات : {ex.Message}";
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
        public async Task<IActionResult> DiagnoseAccountingDocuments()
        {
            try
            {
                var diagnostics = new List<string>();

                // Get all accounting documents
                var allDocuments = await _context.AccountingDocuments
                    .Include(d => d.PayerCustomer)
                    .Include(d => d.ReceiverCustomer)
                    .Where(d => d.IsVerified)
                    .OrderBy(d => d.DocumentDate)
                    .ToListAsync();

                diagnostics.Add($"Total verified accounting documents: {allDocuments.Count}");

                // Get all history records for accounting documents
                var allHistoryRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.AccountingDocument)
                    .OrderBy(h => h.CreatedAt)
                    .ToListAsync();

                diagnostics.Add($"Total history records for accounting documents: {allHistoryRecords.Count}");

                // Get current customer balances
                var customerBalances = await _context.CustomerBalances
                    .Include(cb => cb.Customer)
                    .ToListAsync();

                diagnostics.Add($"Total customer balances: {customerBalances.Count}");

                // Show current balances
                foreach (var balance in customerBalances.Take(10))
                {
                    diagnostics.Add($"Customer {balance.CustomerId}: {balance.Balance:F2} {balance.CurrencyCode}");
                }

                // Group by document ID
                var historyByDocument = allHistoryRecords.GroupBy(h => h.ReferenceId).ToList();
                diagnostics.Add($"Unique documents in history: {historyByDocument.Count}");

                // Check for documents without history
                var documentsWithoutHistory = allDocuments
                    .Where(d => !allHistoryRecords.Any(h => h.ReferenceId == d.Id))
                    .ToList();

                diagnostics.Add($"Documents without history records: {documentsWithoutHistory.Count}");

                // Check for history without documents  
                var historyWithoutDocuments = allHistoryRecords
                    .Where(h => h.ReferenceId.HasValue && !allDocuments.Any(d => d.Id == h.ReferenceId.Value))
                    .ToList();

                diagnostics.Add($"History records without corresponding documents: {historyWithoutDocuments.Count}");

                // Check for correction records already applied
                var correctionRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.Manual &&
                               h.CreatedBy != null && h.CreatedBy.Contains("Accounting Document Logic Correction"))
                    .ToListAsync();

                diagnostics.Add($"Existing correction records: {correctionRecords.Count}");

                // Show recent history records
                var recentHistory = await _context.CustomerBalanceHistory
                    .OrderByDescending(h => h.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                diagnostics.Add("Recent 5 history records:");
                foreach (var history in recentHistory)
                {
                    diagnostics.Add($"  Customer {history.CustomerId}: {history.TransactionAmount:F2} {history.CurrencyCode} ({history.TransactionType}) - Balance: {history.BalanceBefore:F2} -> {history.BalanceAfter:F2}");
                }

                TempData["DiagnosticInfo"] = string.Join("\n", diagnostics);
                TempData["Success"] = "تشخیص کامل شد - نتایج در لاگ";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در تشخیص: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ResetAccountingDocumentVerification()
        {
            try
            {
                var resetLog = new List<string>();

                // Step 1: Find all verified accounting documents (but keep them - just unverify them)
                // EXCLUDE deleted documents from processing
                var verifiedDocuments = await _context.AccountingDocuments
                    .Where(d => d.IsVerified && !d.IsDeleted)
                    .ToListAsync();

                resetLog.Add($"Found {verifiedDocuments.Count} verified accounting documents (excluding deleted)");

                // Step 2: RESET customer balance history (Orders + Documents) 
                // BUT PRESERVE Manual transaction records
                var manualCustomerHistory = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.Manual)
                    .ToListAsync();

                var nonManualCustomerHistory = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType != CustomerBalanceTransactionType.Manual)
                    .ToListAsync();

                _context.CustomerBalanceHistory.RemoveRange(nonManualCustomerHistory);
                resetLog.Add($"Removed {nonManualCustomerHistory.Count} non-manual customer balance history records");
                resetLog.Add($"Preserved {manualCustomerHistory.Count} manual customer balance history records");

                // Step 3: RESET all customer balances to zero
                var allCustomerBalances = await _context.CustomerBalances.ToListAsync();
                foreach (var balance in allCustomerBalances)
                {
                    balance.Balance = 0;
                    balance.LastUpdated = DateTime.UtcNow;
                    balance.Notes = "Reset to zero - will be recalculated";
                }
                resetLog.Add($"Reset {allCustomerBalances.Count} customer balances to zero");

                // Step 4: Reset bank account history and balances (YES - will be regenerated)
                var allBankHistory = await _context.BankAccountBalanceHistory.ToListAsync();
                _context.BankAccountBalanceHistory.RemoveRange(allBankHistory);
                resetLog.Add($"Removed {allBankHistory.Count} bank account balance history records");

                var allBankBalances = await _context.BankAccountBalances.ToListAsync();
                foreach (var balance in allBankBalances)
                {
                    balance.Balance = 0;
                    balance.LastUpdated = DateTime.UtcNow;
                }
                resetLog.Add($"Reset {allBankBalances.Count} bank account balances to zero");

                // Step 4.5: Reset currency pool history and balances (YES - will be regenerated)
                var allPoolHistory = await _context.CurrencyPoolHistory.ToListAsync();
                _context.CurrencyPoolHistory.RemoveRange(allPoolHistory);
                resetLog.Add($"Removed {allPoolHistory.Count} currency pool history records");

                var allPools = await _context.CurrencyPools.ToListAsync();
                foreach (var pool in allPools)
                {
                    pool.Balance = 0;
                    pool.LastUpdated = DateTime.UtcNow;
                }
                resetLog.Add($"Reset {allPools.Count} currency pool balances to zero");

                // Step 5: Set all accounting documents to unverified (but keep the documents)
                foreach (var document in verifiedDocuments)
                {
                    document.IsVerified = false;
                    document.VerifiedAt = null;
                    document.VerifiedBy = null;
                }
                resetLog.Add($"Set {verifiedDocuments.Count} documents to unverified status");

                await _context.SaveChangesAsync();

                // Step 6: Recalculate chronologically - MIXED BY DATE (Orders + Documents together)
                // EXCLUDE deleted orders and documents from recalculation
                var allOrders = await _context.Orders
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => !o.IsDeleted)
                    .ToListAsync();

                var allDocuments = await _context.AccountingDocuments
                    .Where(d => !d.IsDeleted)
                    .ToListAsync();

                // Create a unified list of financial events sorted by date
                var financialEvents = new List<(DateTime Date, string Type, object Item)>();

                // Add all orders with their creation date
                foreach (var order in allOrders)
                {
                    financialEvents.Add((order.CreatedAt, "Order", order));
                }

                // Add all documents with their document date
                foreach (var document in allDocuments)
                {
                    financialEvents.Add((document.DocumentDate, "Document", document));
                }

                // Sort everything by date chronologically
                var sortedEvents = financialEvents.OrderBy(e => e.Date).ToList();

                resetLog.Add($"Recalculating chronologically by DATE: {sortedEvents.Count} total events ({allOrders.Count} orders + {allDocuments.Count} documents)");

                // Process all events in pure chronological order
                foreach (var eventItem in sortedEvents)
                {
                    if (eventItem.Type == "Order")
                    {
                        var order = (Order)eventItem.Item;
                        await _centralFinancialService.ProcessOrderCreationAsync(order, "System - Reset Recalculation");
                        resetLog.Add($"✅ {eventItem.Date:MM/dd HH:mm} | ORDER {order.Id}: {order.FromAmount} {order.FromCurrency.Code} -> {order.ToAmount} {order.ToCurrency.Code}");
                    }
                    else if (eventItem.Type == "Document")
                    {
                        var document = (AccountingDocument)eventItem.Item;

                        // Mark as verified temporarily for processing
                        document.IsVerified = true;
                        document.VerifiedAt = document.DocumentDate;
                        document.VerifiedBy = "System - Reset Recalculation";

                        await _centralFinancialService.ProcessAccountingDocumentAsync(document, "System - Reset Recalculation");

                        resetLog.Add($"✅ {eventItem.Date:MM/dd HH:mm} | DOCUMENT {document.Id}: {document.Amount:N2} {document.CurrencyCode}");
                        resetLog.Add($"   - Payer: Customer {document.PayerCustomerId} gets +{document.Amount}");
                        resetLog.Add($"   - Receiver: Customer {document.ReceiverCustomerId} gets -{document.Amount}");
                    }
                }

                await _context.SaveChangesAsync();

                // Step 7: Prepare summary
                var summary = new[]
                {
                    $"🔄 سوابق مشتری حذف شده: {nonManualCustomerHistory.Count} (حفظ شده: {manualCustomerHistory.Count} دستی)",
                    $"🔄 سوابق بانک حذف شده: {allBankHistory.Count}",
                    $"🔄 سوابق صندوق ارز حذف شده: {allPoolHistory.Count}",
                    $"🔄 موجودی مشتریان صفر شده: {allCustomerBalances.Count}",
                    $"🔄 موجودی بانک‌ها صفر شده: {allBankBalances.Count}",
                    $"🔄 موجودی صندوق‌های ارز صفر شده: {allPools.Count}",
                    $"✅ معاملات مجدداً محاسبه شده: {allOrders.Count} (حذف شده‌ها نادیده گرفته شد)",
                    $"✅ اسناد مجدداً محاسبه شده: {allDocuments.Count} (حذف شده‌ها نادیده گرفته شد)",
                    "",
                    "✅ همه سوابق مالی با منطق صحیح و به ترتیب زمانی بازسازی شدند",
                    "📅 ترتیب: اول معاملات ، سپس اسناد حسابداری",
                    "🎯 منطق صحیح: پرداخت کننده = +مبلغ، دریافت کننده = -مبلغ",
                    "⚠️ رکوردهای دستی (Manual) حفظ شدند",
                    "⚠️ معاملات و اسناد حذف شده (IsDeleted=true) نادیده گرفته شدند"
                };

                TempData["Success"] = string.Join("<br/>", summary);
                TempData["ResetLog"] = string.Join("\n", resetLog);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در بازنشانی اسناد حسابداری: {ex.Message}";
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

        [HttpPost]
        public async Task<IActionResult> FixAllTransactionDates()
        {
            try
            {
                var fixLog = new List<string>();
                int totalFixed = 0;

                // Fix CustomerBalanceHistory transaction dates
                var customerHistory = await _context.CustomerBalanceHistory // Find records where dates are the same
                    .ToListAsync();

                fixLog.Add($"Found {customerHistory.Count} customer balance history records to fix");

                foreach (var history in customerHistory)
                {
                    DateTime? correctDate = null;

                    // If it's an order transaction, get the order creation date
                    if (history.TransactionType == CustomerBalanceTransactionType.Order && history.ReferenceId.HasValue)
                    {
                        var order = await _context.Orders
                            .FirstOrDefaultAsync(o => o.Id == history.ReferenceId.Value);
                        if (order != null)
                        {
                            correctDate = order.CreatedAt;
                        }
                    }
                    // If it's an accounting document transaction, get the document date
                    else if (history.TransactionType == CustomerBalanceTransactionType.AccountingDocument && history.ReferenceId.HasValue)
                    {
                        var document = await _context.AccountingDocuments
                            .FirstOrDefaultAsync(d => d.Id == history.ReferenceId.Value);
                        if (document != null)
                        {
                            correctDate = document.DocumentDate;
                        }
                    }

                    if (correctDate.HasValue)
                    {
                        history.TransactionDate = correctDate.Value;
                        totalFixed++;
                    }
                }

                // Fix CurrencyPoolHistory transaction dates  
                var poolHistory = await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionDate == h.CreatedAt) // Find records where dates are the same
                    .ToListAsync();

                fixLog.Add($"Found {poolHistory.Count} currency pool history records to fix");

                foreach (var history in poolHistory)
                {
                    DateTime? correctDate = null;

                    // If it's an order transaction, get the order creation date
                    if (history.TransactionType == CurrencyPoolTransactionType.Order && history.ReferenceId.HasValue)
                    {
                        var order = await _context.Orders
                            .FirstOrDefaultAsync(o => o.Id == history.ReferenceId.Value);
                        if (order != null)
                        {
                            correctDate = order.CreatedAt;
                        }
                    }

                    if (correctDate.HasValue && correctDate.Value != history.TransactionDate)
                    {
                        history.TransactionDate = correctDate.Value;
                        totalFixed++;
                    }
                }

                // Fix BankAccountBalanceHistory transaction dates
                var bankHistory = await _context.BankAccountBalanceHistory
                    .Where(h => h.TransactionDate == h.CreatedAt) // Find records where dates are the same
                    .ToListAsync();

                fixLog.Add($"Found {bankHistory.Count} bank account balance history records to fix");

                foreach (var history in bankHistory)
                {
                    DateTime? correctDate = null;

                    // If it's a document transaction, get the document date
                    if (history.TransactionType == BankAccountTransactionType.Document && history.ReferenceId.HasValue)
                    {
                        var document = await _context.AccountingDocuments
                            .FirstOrDefaultAsync(d => d.Id == history.ReferenceId.Value);
                        if (document != null)
                        {
                            correctDate = document.DocumentDate;
                        }
                    }

                    if (correctDate.HasValue && correctDate.Value != history.TransactionDate)
                    {
                        history.TransactionDate = correctDate.Value;
                        totalFixed++;
                    }
                }

                // Save all changes
                if (totalFixed > 0)
                {
                    await _context.SaveChangesAsync();
                    fixLog.Add($"Successfully saved {totalFixed} transaction date corrections");
                }
                else
                {
                    fixLog.Add("No transaction dates needed fixing");
                }

                var summary = new[]
                {
                    $"✅ تاریخ {totalFixed} تراکنش اصلاح شد",
                    $"📋 بررسی شد: {customerHistory.Count} تاریخچه مشتری + {poolHistory.Count} تاریخچه صندوق + {bankHistory.Count} تاریخچه بانک",
                    "🎯 منطق اصلاح: معاملات  = Order.CreatedAt، اسناد = Document.DocumentDate",
                    "📅 حالا تاریخ تراکنش = تاریخ واقعی معامله (نه زمان ایجاد رکورد)"
                };

                TempData["Success"] = string.Join("<br/>", summary);
                TempData["FixLog"] = string.Join("\n", fixLog);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در اصلاح تاریخ تراکنش‌ها: {ex.Message}";
            }

            return RedirectToAction("Index");
        }


        /// <summary>
        /// باز محاسبه بر اساس تاریخ تراکنش
        /// </summary>
        /// <returns></returns>

        [HttpPost]
        public async Task<IActionResult> RecalculateAllBalancesFromTransactionDates()
        {
            try
            {
                var recalcLog = new List<string>();
                recalcLog.Add("شروع بازمحاسبه موجودی‌ها بر اساس تاریخ تراکنش‌ها...");

                // Get counts before recalculation
                var customerBalanceCount = await _context.CustomerBalances.CountAsync();
                var poolBalanceCount = await _context.CurrencyPools.CountAsync();
                var bankBalanceCount = await _context.BankAccountBalances.CountAsync();

                var customerHistoryCount = await _context.CustomerBalanceHistory.CountAsync();
                var poolHistoryCount = await _context.CurrencyPoolHistory.CountAsync();
                var bankHistoryCount = await _context.BankAccountBalanceHistory.CountAsync();

                recalcLog.Add($"آمار قبل از بازمحاسبه:");
                recalcLog.Add($"- موجودی مشتری: {customerBalanceCount}");
                recalcLog.Add($"- موجودی صندوق: {poolBalanceCount}");
                recalcLog.Add($"- موجودی بانک: {bankBalanceCount}");
                recalcLog.Add($"- تاریخچه مشتری: {customerHistoryCount}");
                recalcLog.Add($"- تاریخچه صندوق: {poolHistoryCount}");
                recalcLog.Add($"- تاریخچه بانک: {bankHistoryCount}");

                // Perform the recalculation
                await _centralFinancialService.RecalculateAllBalancesFromTransactionDatesAsync("Database Admin");

                recalcLog.Add("✅ بازمحاسبه با موفقیت انجام شد");

                // Get some sample results to verify
                var sampleCustomerBalances = await _context.CustomerBalances
                    .Include(cb => cb.Customer)
                    .Take(5)
                    .ToListAsync();

                var samplePools = await _context.CurrencyPools
                    .Take(5)
                    .ToListAsync();

                recalcLog.Add("نمونه موجودی‌های محاسبه شده:");
                foreach (var balance in sampleCustomerBalances)
                {
                    recalcLog.Add($"- مشتری {balance.Customer?.FullName}: {balance.Balance:F2} {balance.CurrencyCode}");
                }

                foreach (var pool in samplePools)
                {
                    recalcLog.Add($"- صندوق {pool.CurrencyCode}: {pool.Balance:F2}");
                }

                var summary = new[]
                {
                    "✅ بازمحاسبه کامل موجودی‌ها انجام شد",
                    $"📊 {customerHistoryCount + poolHistoryCount + bankHistoryCount} رکورد تاریخچه پردازش شد",
                    "🎯 ترتیب پردازش: بر اساس TransactionDate (تاریخ واقعی تراکنش)",
                    "📈 موجودی‌ها حالا دقیقاً منطبق با ترتیب زمانی واقعی معاملات است",
                    "🔄 تمام رکوردهای تاریخچه نیز بروزرسانی شدند"
                };

                // Check if this is an AJAX request
                bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                                   Request.Headers["Accept"].ToString().Contains("application/json");

                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = true,
                        message = string.Join("\n", summary),
                        log = string.Join("\n", recalcLog)
                    });
                }

                // Return redirect for regular form submissions
                TempData["Success"] = string.Join("<br/>", summary);
                TempData["RecalcLog"] = string.Join("\n", recalcLog);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Check if this is an AJAX request
                bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                                   Request.Headers["Accept"].ToString().Contains("application/json");

                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = false,
                        error = $"خطا در بازمحاسبه موجودی‌ها: {ex.Message}"
                    });
                }

                // Return redirect for regular form submissions
                TempData["Error"] = $"خطا در بازمحاسبه موجودی‌ها: {ex.Message}";
                return RedirectToAction("Index");
            }
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
        public async Task<IActionResult> FixAllCustomerBalances()
        {
            int createdCount = 0;
            try
            {
                var allCustomers = await _context.Customers.Where(c => !c.IsSystem).ToListAsync();
                var allCurrencies = await _context.Currencies.ToListAsync();
                var existingBalances = await _context.CustomerBalances.ToListAsync();

                var newBalances = new List<CustomerBalance>();
                foreach (var customer in allCustomers)
                {
                    foreach (var currency in allCurrencies)
                    {
                        bool exists = existingBalances.Any(cb => cb.CustomerId == customer.Id && cb.CurrencyCode == currency.Code);
                        if (!exists)
                        {
                            newBalances.Add(new CustomerBalance
                            {
                                CustomerId = customer.Id,
                                CurrencyCode = currency.Code,
                                Balance = 0,
                                Notes = "Created by FixAllCustomerBalances admin action"
                            });
                            createdCount++;
                        }
                    }
                }
                if (newBalances.Count > 0)
                {
                    _context.CustomerBalances.AddRange(newBalances);
                    await _context.SaveChangesAsync();
                }
                TempData["Success"] = $"تکمیل شد: {createdCount} رکورد جدید CustomerBalance ایجاد شد.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا در ایجاد CustomerBalance: {ex.Message}";
            }
            return RedirectToAction("Index");
        }





        [HttpPost]
        public async Task<IActionResult> UpdateTransactionNumbers()
        {
            try
            {
                var updateScript = new UpdateTransactionNumbers(_context,
                    LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<UpdateTransactionNumbers>());

                // Generate initial report
                var initialReport = await updateScript.GenerateTransactionNumberCoverageReportAsync();

                var initialLog = new List<string>
                {
                    "📊 گزارش قبل از بروزرسانی:",
                    $"- CustomerBalanceHistory: {initialReport.CustomerBalanceHistoryTotal} کل، {initialReport.CustomerBalanceHistoryWithTransactionNumber} با شماره تراکنش",
                    $"- BankAccountBalanceHistory: {initialReport.BankAccountBalanceHistoryTotal} کل، {initialReport.BankAccountBalanceHistoryWithTransactionNumber} با شماره تراکنش",
                    $"- AccountingDocuments: {initialReport.AccountingDocumentsTotal} کل، {initialReport.AccountingDocumentsWithReferenceNumber} با شماره تراکنش",
                    ""
                };

                // Perform the updates
                await updateScript.UpdateAllHistoryTransactionNumbersAsync();

                // Generate final report
                var finalReport = await updateScript.GenerateTransactionNumberCoverageReportAsync();

                var updateLog = new List<string>
                {
                    "✅ بروزرسانی شماره تراکنش‌ها تکمیل شد:",
                    "",
                    "📈 نتایج نهایی:",
                    $"- CustomerBalanceHistory: {finalReport.CustomerBalanceHistoryWithTransactionNumber} از {finalReport.CustomerBalanceHistoryTotal} ({finalReport.CustomerBalanceHistoryCoveragePercentage:F1}%)",
                    $"- BankAccountBalanceHistory: {finalReport.BankAccountBalanceHistoryWithTransactionNumber} از {finalReport.BankAccountBalanceHistoryTotal} ({finalReport.BankAccountBalanceHistoryCoveragePercentage:F1}%)",
                    $"- AccountingDocuments: {finalReport.AccountingDocumentsWithReferenceNumber} از {finalReport.AccountingDocumentsTotal} ({finalReport.AccountingDocumentsCoveragePercentage:F1}%)",
                    "",
                    "🎯 منطق بروزرسانی:",
                    "- CustomerBalanceHistory با TransactionType=AccountingDocument ← AccountingDocument.ReferenceNumber",
                    "- BankAccountBalanceHistory با TransactionType=Document ← AccountingDocument.ReferenceNumber",
                    "",
                    "✨ حالا تمام سوابق تراکنش شماره تراکنش مناسب دارند"
                };

                // Calculate improvements
                var customerImprovement = finalReport.CustomerBalanceHistoryWithTransactionNumber - initialReport.CustomerBalanceHistoryWithTransactionNumber;
                var bankImprovement = finalReport.BankAccountBalanceHistoryWithTransactionNumber - initialReport.BankAccountBalanceHistoryWithTransactionNumber;

                if (customerImprovement > 0 || bankImprovement > 0)
                {
                    updateLog.Add("");
                    updateLog.Add($"📊 بهبودها:");
                    if (customerImprovement > 0)
                        updateLog.Add($"- CustomerBalanceHistory: +{customerImprovement} رکورد بروزرسانی شد");
                    if (bankImprovement > 0)
                        updateLog.Add($"- BankAccountBalanceHistory: +{bankImprovement} رکورد بروزرسانی شد");
                }
                else
                {
                    updateLog.Add("");
                    updateLog.Add("ℹ️ هیچ رکورد جدیدی نیاز به بروزرسانی نداشت - همه چیز از قبل به‌روز بود");
                }

                // Check if this is an AJAX request
                bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                                   Request.Headers["Accept"].ToString().Contains("application/json");

                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = true,
                        message = "بروزرسانی شماره تراکنش‌ها با موفقیت انجام شد",
                        initialReport = initialReport,
                        finalReport = finalReport,
                        improvements = new { customer = customerImprovement, bank = bankImprovement }
                    });
                }

                TempData["Success"] = string.Join("<br/>", updateLog);
                TempData["InitialReport"] = string.Join("\n", initialLog);
            }
            catch (Exception ex)
            {
                bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                                   Request.Headers["Accept"].ToString().Contains("application/json");

                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = false,
                        error = $"خطا در بروزرسانی شماره تراکنش‌ها: {ex.Message}"
                    });
                }

                TempData["Error"] = $"خطا در بروزرسانی شماره تراکنش‌ها: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactionNumberReport()
        {
            try
            {
                var updateScript = new UpdateTransactionNumbers(_context,
                    LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<UpdateTransactionNumbers>());

                var report = await updateScript.GenerateTransactionNumberCoverageReportAsync();

                // Check if this is an AJAX request
                bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                                   Request.Headers["Accept"].ToString().Contains("application/json");

                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = true,
                        report = report,
                        summary = new
                        {
                            customerCoverage = $"{report.CustomerBalanceHistoryWithTransactionNumber}/{report.CustomerBalanceHistoryTotal} ({report.CustomerBalanceHistoryCoveragePercentage:F1}%)",
                            bankCoverage = $"{report.BankAccountBalanceHistoryWithTransactionNumber}/{report.BankAccountBalanceHistoryTotal} ({report.BankAccountBalanceHistoryCoveragePercentage:F1}%)",
                            documentCoverage = $"{report.AccountingDocumentsWithReferenceNumber}/{report.AccountingDocumentsTotal} ({report.AccountingDocumentsCoveragePercentage:F1}%)"
                        }
                    });
                }

                var reportLines = new[]
                {
                    "📊 گزارش پوشش شماره تراکنش:",
                    "",
                    "👥 CustomerBalanceHistory (نوع سند حسابداری):",
                    $"- کل رکوردها: {report.CustomerBalanceHistoryTotal:N0}",
                    $"- با شماره تراکنش: {report.CustomerBalanceHistoryWithTransactionNumber:N0}",
                    $"- بدون شماره تراکنش: {report.CustomerBalanceHistoryWithoutTransactionNumber:N0}",
                    $"- پوشش: {report.CustomerBalanceHistoryCoveragePercentage:F1}%",
                    "",
                    "🏦 BankAccountBalanceHistory (نوع سند):",
                    $"- کل رکوردها: {report.BankAccountBalanceHistoryTotal:N0}",
                    $"- با شماره تراکنش: {report.BankAccountBalanceHistoryWithTransactionNumber:N0}",
                    $"- بدون شماره تراکنش: {report.BankAccountBalanceHistoryWithoutTransactionNumber:N0}",
                    $"- پوشش: {report.BankAccountBalanceHistoryCoveragePercentage:F1}%",
                    "",
                    "📄 AccountingDocuments:",
                    $"- کل اسناد: {report.AccountingDocumentsTotal:N0}",
                    $"- با شماره تراکنش: {report.AccountingDocumentsWithReferenceNumber:N0}",
                    $"- بدون شماره تراکنش: {report.AccountingDocumentsWithoutReferenceNumber:N0}",
                    $"- پوشش: {report.AccountingDocumentsCoveragePercentage:F1}%"
                };

                TempData["Info"] = string.Join("<br/>", reportLines);
            }
            catch (Exception ex)
            {
                bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                                   Request.Headers["Accept"].ToString().Contains("application/json");

                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = false,
                        error = $"خطا در تولید گزارش: {ex.Message}"
                    });
                }

                TempData["Error"] = $"خطا در تولید گزارش شماره تراکنش‌ها: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ApplyDatabaseRounding()
        {
            try
            {
                // WARNING: This is a destructive operation.
                // It's recommended to secure this endpoint or remove it after use.
                var resultSummary = await ForexExchange.Helpers.DatabaseRoundingHelper.ApplyRoundingToAllDataAsync(_context);

                return Json(new { success = true, message = "Database rounding process completed successfully.", summary = resultSummary });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = $"An error occurred during the rounding process: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> FixIRROrdersEndingIn1000()
        {
            try
            {
                var fixLog = new List<string>();
                int ordersFixed = 0;
                int customerHistoryFixed = 0;
                int poolHistoryFixed = 0;
                int bankHistoryFixed = 0;

                fixLog.Add("🔧 اصلاح جامع رقم هزارگان برابر 1 در ارز IRR");
                fixLog.Add("این عملیات تمام جداول مالی را برای رفع مشکل رگرد ریال بررسی و اصلاح می‌کند");
                fixLog.Add(""); 

                // 1. Fix Orders with IRR amounts ending in 1000 (thousands digit = 1)
                var ordersToFix = await _context.Orders
                    .Include(o => o.ToCurrency)
                    .Include(o => o.FromCurrency)
                    .Where(o => (o.ToCurrency.Code == "IRR" && ((long)o.ToAmount / 1000) % 10 == 1) ||
                               (o.FromCurrency.Code == "IRR" && ((long)o.FromAmount / 1000) % 10 == 1))
                    .ToListAsync();

                fixLog.Add("🔧 اصلاح معاملات (Orders):");
                foreach (var order in ordersToFix)
                {
                    bool orderFixed = false;
                    
                    // Fix ToAmount if IRR
                    if (order.ToCurrency.Code == "IRR" && ((long)order.ToAmount / 1000) % 10 == 1)
                    {
                        var originalAmount = order.ToAmount;
                        var newAmount = originalAmount - 1000;
                        order.ToAmount = newAmount;
                        fixLog.Add($"  Order ID {order.Id} (ToAmount): {originalAmount:N0} → {newAmount:N0}");
                        orderFixed = true;
                    }
                    
                    // Fix FromAmount if IRR
                    if (order.FromCurrency.Code == "IRR" && ((long)order.FromAmount / 1000) % 10 == 1)
                    {
                        var originalAmount = order.FromAmount;
                        var newAmount = originalAmount - 1000;
                        order.FromAmount = newAmount;
                        fixLog.Add($"  Order ID {order.Id} (FromAmount): {originalAmount:N0} → {newAmount:N0}");
                        orderFixed = true;
                    }
                    
                    if (orderFixed) ordersFixed++;
                }

                // 2. Fix CustomerBalanceHistory with IRR amounts ending in 1000
                var customerHistoryToFix = await _context.CustomerBalanceHistory
                    .Where(h => h.CurrencyCode == "IRR" && 
                               (((long)h.TransactionAmount / 1000) % 10 == 1 || 
                                ((long)h.TransactionAmount / 1000) % 10 == -1))
                    .ToListAsync();

                fixLog.Add("");
                fixLog.Add("👥 اصلاح تاریخچه موجودی مشتریان (CustomerBalanceHistory):");
                foreach (var history in customerHistoryToFix)
                {
                    var originalAmount = history.TransactionAmount;
                    var absAmount = originalAmount >= 0 ? originalAmount : -originalAmount;
                    var thousandsDigit = ((long)absAmount / 1000) % 10;
                    
                    if (thousandsDigit == 1)
                    {
                        var newAbsAmount = absAmount - 1000;
                        var newAmount = originalAmount >= 0 ? newAbsAmount : -newAbsAmount;
                        
                        history.TransactionAmount = newAmount;
                        fixLog.Add($"  History ID {history.Id}: {originalAmount:N0} → {newAmount:N0}");
                        customerHistoryFixed++;
                    }
                }

                // 3. Fix CurrencyPoolHistory with IRR amounts ending in 1000
                var poolHistoryToFix = await _context.CurrencyPoolHistory
                    .Where(h => h.CurrencyCode == "IRR" && 
                               (((long)h.TransactionAmount / 1000) % 10 == 1 || 
                                ((long)h.TransactionAmount / 1000) % 10 == -1))
                    .ToListAsync();

                fixLog.Add("");
                fixLog.Add("🏊 اصلاح تاریخچه استخر ارز (CurrencyPoolHistory):");
                foreach (var poolHistory in poolHistoryToFix)
                {
                    var originalAmount = poolHistory.TransactionAmount;
                    var absAmount = originalAmount >= 0 ? originalAmount : -originalAmount;
                    var thousandsDigit = ((long)absAmount / 1000) % 10;
                    
                    if (thousandsDigit == 1)
                    {
                        var newAbsAmount = absAmount - 1000;
                        var newAmount = originalAmount >= 0 ? newAbsAmount : -newAbsAmount;
                        
                        poolHistory.TransactionAmount = newAmount;
                        fixLog.Add($"  PoolHistory ID {poolHistory.Id}: {originalAmount:N0} → {newAmount:N0}");
                        poolHistoryFixed++;
                    }
                }

                // 4. Fix BankAccountBalanceHistory with IRR amounts ending in 1000
                var bankHistoryToFix = await _context.BankAccountBalanceHistory
                    .Include(h => h.BankAccount)
                    .Where(h => h.BankAccount.CurrencyCode == "IRR" && 
                               (((long)h.TransactionAmount / 1000) % 10 == 1 || 
                                ((long)h.TransactionAmount / 1000) % 10 == -1 ||
                                ((long)h.BalanceBefore / 1000) % 10 == 1 ||
                                ((long)h.BalanceBefore / 1000) % 10 == -1 ||
                                ((long)h.BalanceAfter / 1000) % 10 == 1 ||
                                ((long)h.BalanceAfter / 1000) % 10 == -1))
                    .ToListAsync();

                fixLog.Add("");
                fixLog.Add("🏦 اصلاح تاریخچه موجودی حساب‌های بانکی (BankAccountBalanceHistory):");
                foreach (var bankHistory in bankHistoryToFix)
                {
                    bool recordFixed = false;
                    var changes = new List<string>();
                    
                    // Fix TransactionAmount
                    var originalTransactionAmount = bankHistory.TransactionAmount;
                    var absTransactionAmount = originalTransactionAmount >= 0 ? originalTransactionAmount : -originalTransactionAmount;
                    var transactionThousandsDigit = ((long)absTransactionAmount / 1000) % 10;
                    
                    if (transactionThousandsDigit == 1)
                    {
                        var newAbsAmount = absTransactionAmount - 1000;
                        var newTransactionAmount = originalTransactionAmount >= 0 ? newAbsAmount : -newAbsAmount;
                        bankHistory.TransactionAmount = newTransactionAmount;
                        changes.Add($"TransactionAmount: {originalTransactionAmount:N0} → {newTransactionAmount:N0}");
                        recordFixed = true;
                    }
                    
                    // Fix BalanceBefore
                    var originalBalanceBefore = bankHistory.BalanceBefore;
                    var absBalanceBefore = originalBalanceBefore >= 0 ? originalBalanceBefore : -originalBalanceBefore;
                    var beforeThousandsDigit = ((long)absBalanceBefore / 1000) % 10;
                    
                    if (beforeThousandsDigit == 1)
                    {
                        var newAbsAmount = absBalanceBefore - 1000;
                        var newBalanceBefore = originalBalanceBefore >= 0 ? newAbsAmount : -newAbsAmount;
                        bankHistory.BalanceBefore = newBalanceBefore;
                        changes.Add($"BalanceBefore: {originalBalanceBefore:N0} → {newBalanceBefore:N0}");
                        recordFixed = true;
                    }
                    
                    // Fix BalanceAfter
                    var originalBalanceAfter = bankHistory.BalanceAfter;
                    var absBalanceAfter = originalBalanceAfter >= 0 ? originalBalanceAfter : -originalBalanceAfter;
                    var afterThousandsDigit = ((long)absBalanceAfter / 1000) % 10;
                    
                    if (afterThousandsDigit == 1)
                    {
                        var newAbsAmount = absBalanceAfter - 1000;
                        var newBalanceAfter = originalBalanceAfter >= 0 ? newAbsAmount : -newAbsAmount;
                        bankHistory.BalanceAfter = newBalanceAfter;
                        changes.Add($"BalanceAfter: {originalBalanceAfter:N0} → {newBalanceAfter:N0}");
                        recordFixed = true;
                    }
                    
                    if (recordFixed)
                    {
                        fixLog.Add($"  BankHistory ID {bankHistory.Id}: {string.Join(", ", changes)}");
                        bankHistoryFixed++;
                    }
                }

                // Save all changes
                await _context.SaveChangesAsync();

                var totalFixed = ordersFixed + customerHistoryFixed + poolHistoryFixed + bankHistoryFixed;
                var summaryMessage = $"✅ اصلاح کامل رقم هزارگان برابر 1 در ارز IRR: {totalFixed} رکورد کل اصلاح شد";
                
                if (totalFixed == 0)
                {
                    summaryMessage = "ℹ️ هیچ رکوردی با رقم هزارگان برابر 1 در ارز IRR یافت نشد";
                }

                fixLog.Insert(0, "📊 خلاصه نتایج:");
                fixLog.Insert(1, $"- معاملات: {ordersFixed} رکورد");
                fixLog.Insert(2, $"- تاریخچه مشتری: {customerHistoryFixed} رکورد");
                fixLog.Insert(3, $"- تاریخچه استخر: {poolHistoryFixed} رکورد");
                fixLog.Insert(4, $"- تاریخچه بانک: {bankHistoryFixed} رکورد");
                fixLog.Insert(5, $"- مجموع: {totalFixed} رکورد");
                fixLog.Insert(6, "");

                var logText = string.Join("\n", fixLog);
                
                var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                if (isAjaxRequest)
                {
                    return Json(new
                    {
                        success = true,
                        message = summaryMessage,
                        log = logText,
                        summary = new
                        {
                            ordersFixed = ordersFixed,
                            customerHistoryFixed = customerHistoryFixed,
                            poolHistoryFixed = poolHistoryFixed,
                            bankHistoryFixed = bankHistoryFixed,
                            totalFixed = totalFixed
                        }
                    });
                }

                TempData["Success"] = summaryMessage;
                TempData["FixLog"] = logText;
            }
            catch (Exception ex)
            {
                var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                if (isAjaxRequest)
                {
                    return Json(new { success = false, error = $"خطا در اصلاح معاملات IRR: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در اصلاح معاملات IRR: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> FixIRROrdersWithReversedRates()
        {
            try
            {
                var fixLog = new List<string>();
                int ordersFixed = 0;
                const decimal RATE_THRESHOLD = 0.001m; // Rates below this are likely reversed

                fixLog.Add("🔄 اصلاح معاملات با نرخ معکوس IRR");
                fixLog.Add($"جستجو برای معاملات با FromCurrency=IRR و نرخ کمتر از {RATE_THRESHOLD}");
                fixLog.Add(""); 

                // Find orders where FromCurrency is IRR and rate is suspiciously low (likely reversed)
                var ordersToFix = await _context.Orders
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => o.FromCurrency.Code == "IRR" && o.Rate < RATE_THRESHOLD && o.Rate > 0)
                    .ToListAsync();

                fixLog.Add($"🔍 یافت شد: {ordersToFix.Count} معامله با نرخ مشکوک");
                fixLog.Add("");

                if (!ordersToFix.Any())
                {
                    var noResultMessage = "هیچ معامله‌ای با FromCurrency=IRR و نرخ معکوس یافت نشد";
                    fixLog.Add($"ℹ️ {noResultMessage}");
                    
                    var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                    if (isAjaxRequest)
                    {
                        return Json(new { 
                            success = true, 
                            message = noResultMessage,
                            fixedCount = 0,
                            log = string.Join("\n", fixLog)
                        });
                    }

                    TempData["Info"] = noResultMessage;
                    return RedirectToAction("Index");
                }

                fixLog.Add("🔧 اصلاح نرخ‌های معکوس:");

                foreach (var order in ordersToFix)
                {
                    var originalRate = order.Rate;
                    var newRate = 1 / originalRate; // Reverse the rate
                    
                    // Update the rate
                    order.Rate = newRate;
                    
                    // Log the change with currency pair info
                    var currencyPair = $"{order.FromCurrency?.Code}/{order.ToCurrency?.Code}";
                    fixLog.Add($"  Order ID {order.Id} ({currencyPair}): Rate {originalRate:F15} → {newRate:F2}");
                    ordersFixed++;
                }

                // Save all changes
                await _context.SaveChangesAsync();

                var summaryMessage = ordersFixed > 0 
                    ? $"✅ {ordersFixed} معامله IRR با نرخ معکوس اصلاح شد"
                    : "ℹ️ هیچ معامله‌ای نیاز به اصلاح نداشت";

                fixLog.Insert(6, "📊 خلاصه نتایج:");
                fixLog.Insert(7, $"- معاملات اصلاح شده: {ordersFixed} رکورد");
                fixLog.Insert(8, $"- آستانه نرخ: کمتر از {RATE_THRESHOLD}");
                fixLog.Insert(9, "- منطق: نرخ جدید = 1 ÷ نرخ قدیمی");
                fixLog.Insert(10, "");

                var logText = string.Join("\n", fixLog);
                
                var isAjaxRequestFinal = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                if (isAjaxRequestFinal)
                {
                    return Json(new
                    {
                        success = true,
                        message = summaryMessage,
                        log = logText,
                        summary = new
                        {
                            ordersFixed = ordersFixed,
                            rateThreshold = RATE_THRESHOLD,
                            totalFound = ordersToFix.Count
                        }
                    });
                }

                TempData["Success"] = summaryMessage;
                TempData["FixLog"] = logText;
            }
            catch (Exception ex)
            {
                var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                if (isAjaxRequest)
                {
                    return Json(new { success = false, error = $"خطا در اصلاح نرخ‌های معکوس IRR: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در اصلاح نرخ‌های معکوس IRR: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CleanCustomerBalances()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var userId = user?.Id;

                // Get all customer balances
                var customerBalances = await _context.CustomerBalances.ToListAsync();
                
                if (customerBalances.Any())
                {
                    // Set all balances to zero
                    foreach (var balance in customerBalances)
                    {
                        balance.Balance = 0;
                        balance.LastUpdated = DateTime.Now;
                    }

                    await _context.SaveChangesAsync();

                    // Send notification
                    await _notificationHub.SendCustomNotificationAsync(
                        "🔄 پاک‌سازی موجودی مشتریان",
                        $"تمام موجودی‌های مشتریان ({customerBalances.Count} رکورد) به صفر تنظیم شد",
                        NotificationEventType.SystemMaintenance,
                        userId,
                        "/Database"
                    );

                    var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                    if (isAjaxRequest)
                    {
                        return Json(new { success = true, message = $"موجودی {customerBalances.Count} مشتری با موفقیت پاک شد" });
                    }

                    TempData["Success"] = $"موجودی {customerBalances.Count} مشتری با موفقیت پاک شد";
                }
                else
                {
                    var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                    if (isAjaxRequest)
                    {
                        return Json(new { success = true, message = "هیچ موجودی مشتری برای پاک کردن یافت نشد" });
                    }

                    TempData["Info"] = "هیچ موجودی مشتری برای پاک کردن یافت نشد";
                }
            }
            catch (Exception ex)
            {
                var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                if (isAjaxRequest)
                {
                    return Json(new { success = false, error = $"خطا در پاک‌سازی موجودی مشتریان: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در پاک‌سازی موجودی مشتریان: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CleanBankAccountBalances()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var userId = user?.Id;

                // Get all bank accounts
                var bankAccounts = await _context.BankAccounts.ToListAsync();
                
                // Get all bank account balances (separate table)
                var bankAccountBalances = await _context.BankAccountBalances.ToListAsync();
                
                int totalCleaned = 0;

                if (bankAccounts.Any())
                {
                    // Set all AccountBalance properties to zero
                    foreach (var account in bankAccounts)
                    {
                        account.AccountBalance = 0;
                        totalCleaned++;
                    }
                }

                if (bankAccountBalances.Any())
                {
                    // Set all BankAccountBalance records to zero
                    foreach (var balance in bankAccountBalances)
                    {
                        balance.Balance = 0;
                        balance.LastUpdated = DateTime.Now;
                        totalCleaned++;
                    }
                }

                if (totalCleaned > 0)
                {
                    await _context.SaveChangesAsync();

                    // Send notification
                    await _notificationHub.SendCustomNotificationAsync(
                        "🔄 پاک‌سازی موجودی حساب‌های بانکی",
                        $"تمام موجودی‌های حساب‌های بانکی پاک شد - {bankAccounts.Count} حساب بانکی و {bankAccountBalances.Count} رکورد موجودی به صفر تنظیم شد",
                        NotificationEventType.SystemMaintenance,
                        userId,
                        "/Database"
                    );

                    var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                    if (isAjaxRequest)
                    {
                        return Json(new { success = true, message = $"موجودی {bankAccounts.Count} حساب بانکی و {bankAccountBalances.Count} رکورد موجودی با موفقیت پاک شد" });
                    }

                    TempData["Success"] = $"موجودی {bankAccounts.Count} حساب بانکی و {bankAccountBalances.Count} رکورد موجودی با موفقیت پاک شد";
                }
                else
                {
                    var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                    if (isAjaxRequest)
                    {
                        return Json(new { success = true, message = "هیچ حساب بانکی یا رکورد موجودی برای پاک کردن یافت نشد" });
                    }

                    TempData["Info"] = "هیچ حساب بانکی یا رکورد موجودی برای پاک کردن یافت نشد";
                }
            }
            catch (Exception ex)
            {
                var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                if (isAjaxRequest)
                {
                    return Json(new { success = false, error = $"خطا در پاک‌سازی موجودی حساب‌های بانکی: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در پاک‌سازی موجودی حساب‌های بانکی: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}
