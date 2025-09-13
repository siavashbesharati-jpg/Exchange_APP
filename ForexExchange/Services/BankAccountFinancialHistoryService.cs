using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ForexExchange.Services
{
    /// <summary>
    /// Bank Account Financial History Service - Database-driven timeline for Bank Account transactions
    /// سرویس تاریخچه مالی حساب بانکی - جدول زمانی مبتنی بر پایگاه داده برای تراکنش‌های حساب بانکی
    /// 
    /// Uses BankAccountBalanceHistory table as the single source of truth
    /// از جدول BankAccountBalanceHistory به عنوان منبع واحد حقیقت استفاده می‌کند
    /// </summary>
    public class BankAccountFinancialHistoryService
    {
        private readonly ForexDbContext _context;

        public BankAccountFinancialHistoryService(ForexDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets bank account financial timeline from BankAccountBalanceHistory table
        /// دریافت جدول زمانی مالی حساب بانکی از جدول BankAccountBalanceHistory
        /// </summary>
        public async Task<List<BankAccountTimelineItem>> GetBankAccountTimelineAsync(
            int? bankAccountId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            try
            {
                // Build query for BankAccountBalanceHistory
                var query = _context.BankAccountBalanceHistory.AsQueryable();

                // Apply bank account filter
                if (bankAccountId.HasValue)
                {
                    query = query.Where(h => h.BankAccountId == bankAccountId.Value);
                }

                // Apply date filter
                if (fromDate.HasValue)
                {
                    query = query.Where(h => h.TransactionDate >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    var endDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(h => h.TransactionDate <= endDate);
                }

                // Get history records ordered by date
                var historyRecords = await query
                    .Include(h => h.BankAccount)
                    .OrderBy(h => h.TransactionDate)
                    .ThenBy(h => h.Id)
                    .ToListAsync();

                if (!historyRecords.Any())
                    return new List<BankAccountTimelineItem>();

                var timelineItems = new List<BankAccountTimelineItem>();

                // Add initial balance entry (using BalanceBefore from first record)
                var firstRecord = historyRecords.First();
                timelineItems.Add(new BankAccountTimelineItem
                {
                    Date = firstRecord.TransactionDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                    Time = firstRecord.TransactionDate.ToString("HH:mm", CultureInfo.InvariantCulture),
                    TransactionType = "Initial",
                    Description = "موجودی اولیه",
                    BankAccountId = firstRecord.BankAccountId,
                    BankAccountName = GetCleanBankAccountName(firstRecord.BankAccount),
                    Amount = 0,
                    Balance = firstRecord.BalanceBefore,
                    ReferenceId = null,
                    CanNavigate = false
                });

                // Convert history records to timeline items
                foreach (var record in historyRecords)
                {
                    var item = new BankAccountTimelineItem
                    {
                        Date = record.TransactionDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                        Time = record.TransactionDate.ToString("HH:mm", CultureInfo.InvariantCulture),
                        TransactionType = record.TransactionType.ToString(),
                        Description = record.Description ?? GetTransactionDescription(record),
                        BankAccountId = record.BankAccountId,
                        BankAccountName = GetCleanBankAccountName(record.BankAccount),
                        Amount = record.TransactionAmount,
                        Balance = record.BalanceAfter,
                        ReferenceId = record.ReferenceId,
                        CanNavigate = record.TransactionType == BankAccountTransactionType.Document && record.ReferenceId.HasValue
                    };

                    timelineItems.Add(item);
                }

                return timelineItems;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading bank account timeline: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get transaction description based on type and data
        /// دریافت توضیحات تراکنش بر اساس نوع و داده‌ها
        /// </summary>
        private string GetTransactionDescription(BankAccountBalanceHistory record)
        {
            return record.TransactionType switch
            {
                BankAccountTransactionType.Document => $"سند حسابداری شماره {record.ReferenceId}",
                BankAccountTransactionType.ManualEdit => "ویرایش دستی موجودی",
                _ => "تراکنش نامشخص"
            };
        }

        /// <summary>
        /// Get clean bank account name without "سیستم صرافی"
        /// دریافت نام تمیز حساب بانکی بدون "سیستم صرافی"
        /// </summary>
        private string GetCleanBankAccountName(BankAccount? bankAccount)
        {
            if (bankAccount == null) return "نامشخص";
            
            var accountName = bankAccount.AccountHolderName == "سیستم صرافی" 
                ? bankAccount.BankName 
                : bankAccount.AccountHolderName;
                
            return (accountName ?? "نامشخص") + " (" + bankAccount.CurrencyCode + ")";
        }

        /// <summary>
        /// Get bank account summary statistics
        /// دریافت آمار خلاصه حساب بانکی
        /// </summary>
        public async Task<BankAccountSummary> GetBankAccountSummaryAsync(int? bankAccountId = null)
        {
            try
            {
                var query = _context.BankAccountBalanceHistory.AsQueryable();

                if (bankAccountId.HasValue)
                {
                    query = query.Where(h => h.BankAccountId == bankAccountId.Value);
                }

                var today = DateTime.Today;
                var totalTransactions = await query.CountAsync();
                var todayTransactions = await query.CountAsync(h => h.TransactionDate.Date == today);

                // Get current balance from latest record for each bank account
                var latestBalances = await _context.BankAccountBalanceHistory
                    .Where(h => !bankAccountId.HasValue || h.BankAccountId == bankAccountId.Value)
                    .Include(h => h.BankAccount)
                    .GroupBy(h => h.BankAccountId)
                    .Select(g => new
                    {
                        BankAccountId = g.Key,
                        BankAccountName = g.First().BankAccount!.AccountHolderName + " - " + g.First().BankAccount!.BankName,
                        Balance = g.OrderByDescending(h => h.TransactionDate)
                                  .ThenByDescending(h => h.Id)
                                  .First().BalanceAfter
                    })
                    .ToListAsync();

                return new BankAccountSummary
                {
                    TotalTransactions = totalTransactions,
                    TodayTransactions = todayTransactions,
                    AccountBalances = latestBalances.ToDictionary(
                        b => b.BankAccountId, 
                        b => (dynamic)new { Name = b.BankAccountName, Balance = b.Balance }
                    ),
                    LastUpdateTime = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading bank account summary: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get all bank accounts for dropdown/selection
        /// دریافت همه حساب‌های بانکی برای کشویی/انتخاب
        /// </summary>
        public async Task<List<BankAccountOption>> GetBankAccountOptionsAsync()
        {
            try
            {
                var bankAccounts = await _context.BankAccounts
                    .Where(ba => ba.IsActive) // Only active accounts
                    .OrderBy(ba => ba.BankName)
                    .ThenBy(ba => ba.AccountHolderName)
                    .Select(ba => new BankAccountOption
                    {
                        Id = ba.Id,
                        Name = (ba.AccountHolderName != "سیستم صرافی" ? ba.AccountHolderName : ba.BankName) + " (" + ba.CurrencyCode + ")",
                        BankName = ba.BankName ?? "نامشخص"
                    })
                    .ToListAsync();

                return bankAccounts;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading bank account options: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Bank Account Timeline Item for display
    /// آیتم جدول زمانی حساب بانکی برای نمایش
    /// </summary>
    public class BankAccountTimelineItem
    {
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int BankAccountId { get; set; }
        public string BankAccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public int? ReferenceId { get; set; }
        public bool CanNavigate { get; set; }
    }

    /// <summary>
    /// Bank Account Summary Statistics
    /// آمار خلاصه حساب بانکی
    /// </summary>
    public class BankAccountSummary
    {
        public int TotalTransactions { get; set; }
        public int TodayTransactions { get; set; }
        public Dictionary<int, dynamic> AccountBalances { get; set; } = new();
        public DateTime LastUpdateTime { get; set; }
    }

    /// <summary>
    /// Bank Account Option for dropdowns
    /// گزینه حساب بانکی برای کشویی‌ها
    /// </summary>
    public class BankAccountOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
    }
}
