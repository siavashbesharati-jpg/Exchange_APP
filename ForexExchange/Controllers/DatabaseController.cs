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
                var verifiedDocuments = await _context.AccountingDocuments
                    .Where(d => d.IsVerified)
                    .ToListAsync();

                resetLog.Add($"Found {verifiedDocuments.Count} verified accounting documents");

                // Step 2: COMPLETELY RESET all customer balance history (Orders + Documents)
                var allCustomerHistory = await _context.CustomerBalanceHistory.ToListAsync();
                _context.CustomerBalanceHistory.RemoveRange(allCustomerHistory);
                resetLog.Add($"Removed {allCustomerHistory.Count} customer balance history records");

                // Step 3: RESET all customer balances to zero
                var allCustomerBalances = await _context.CustomerBalances.ToListAsync();
                foreach (var balance in allCustomerBalances)
                {
                    balance.Balance = 0;
                    balance.LastUpdated = DateTime.UtcNow;
                    balance.Notes = "Reset to zero - will be recalculated";
                }
                resetLog.Add($"Reset {allCustomerBalances.Count} customer balances to zero");

                // Step 4: Reset bank account history and balances
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
                var allOrders = await _context.Orders
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .ToListAsync();

                var allDocuments = await _context.AccountingDocuments
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
                    $"🔄 سوابق مشتری حذف شده: {allCustomerHistory.Count}",
                    $"🔄 سوابق بانک حذف شده: {allBankHistory.Count}",
                    $"🔄 موجودی مشتریان صفر شده: {allCustomerBalances.Count}",
                    $"🔄 موجودی بانک‌ها صفر شده: {allBankBalances.Count}",
                    $"✅ سفارشات مجدداً محاسبه شده: {allOrders.Count}",
                    $"✅ اسناد مجدداً محاسبه شده: {allDocuments.Count}",
                    "",
                    "✅ همه سوابق مالی با منطق صحیح و به ترتیب زمانی بازسازی شدند",
                    "📅 ترتیب: اول سفارشات، سپس اسناد حسابداری",
                    "🎯 منطق صحیح: پرداخت کننده = +مبلغ، دریافت کننده = -مبلغ"
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
    }
}
