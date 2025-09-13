using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ForexExchange.Services
{
    /// <summary>
    /// Pool Financial History Service - Database-driven timeline for Currency Pool transactions
    /// سرویس تاریخچه مالی پول - جدول زمانی مبتنی بر پایگاه داده برای تراکنش‌های پول ارزها
    /// 
    /// Uses CurrencyPoolHistory table as the single source of truth
    /// از جدول CurrencyPoolHistory به عنوان منبع واحد حقیقت استفاده می‌کند
    /// </summary>
    public class PoolFinancialHistoryService
    {
        private readonly ForexDbContext _context;

        public PoolFinancialHistoryService(ForexDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets currency pool financial timeline from CurrencyPoolHistory table
        /// دریافت جدول زمانی مالی پول ارز از جدول CurrencyPoolHistory
        /// </summary>
        public async Task<List<PoolTimelineItem>> GetPoolTimelineAsync(
            string? currencyCode = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            try
            {
                // Build query for CurrencyPoolHistory
                var query = _context.CurrencyPoolHistory.AsQueryable();

                // Apply currency filter
                if (!string.IsNullOrEmpty(currencyCode))
                {
                    query = query.Where(h => h.CurrencyCode == currencyCode);
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
                    .OrderBy(h => h.TransactionDate)
                    .ThenBy(h => h.Id)
                    .ToListAsync();

                if (!historyRecords.Any())
                    return new List<PoolTimelineItem>();

                var timelineItems = new List<PoolTimelineItem>();

                // Add initial balance entry (using BalanceBefore from first record)
                var firstRecord = historyRecords.First();
                timelineItems.Add(new PoolTimelineItem
                {
                    Date = firstRecord.TransactionDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                    Time = firstRecord.TransactionDate.ToString("HH:mm", CultureInfo.InvariantCulture),
                    TransactionType = "Initial",
                    Description = "موجودی اولیه",
                    CurrencyCode = firstRecord.CurrencyCode,
                    Amount = 0,
                    Balance = firstRecord.BalanceBefore,
                    ReferenceId = null,
                    CanNavigate = false
                });

                // Convert history records to timeline items
                foreach (var record in historyRecords)
                {
                    var item = new PoolTimelineItem
                    {
                        Date = record.TransactionDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                        Time = record.TransactionDate.ToString("HH:mm", CultureInfo.InvariantCulture),
                        TransactionType = record.TransactionType,
                        Description = record.Description ?? GetTransactionDescription(record),
                        CurrencyCode = record.CurrencyCode,
                        Amount = record.TransactionAmount,
                        Balance = record.BalanceAfter,
                        ReferenceId = record.ReferenceId,
                        CanNavigate = record.TransactionType == "Order" && record.ReferenceId.HasValue
                    };

                    timelineItems.Add(item);
                }

                return timelineItems;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading pool timeline: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get transaction description based on type and data
        /// دریافت توضیحات تراکنش بر اساس نوع و داده‌ها
        /// </summary>
        private string GetTransactionDescription(CurrencyPoolHistory record)
        {
            return record.TransactionType switch
            {
                "Order" => $"سفارش شماره {record.ReferenceId}",
                "ManualEdit" => "ویرایش دستی موجودی",
                _ => "تراکنش نامشخص"
            };
        }

        /// <summary>
        /// Get pool summary statistics
        /// دریافت آمار خلاصه پول
        /// </summary>
        public async Task<PoolSummary> GetPoolSummaryAsync(string? currencyCode = null)
        {
            try
            {
                var query = _context.CurrencyPoolHistory.AsQueryable();

                if (!string.IsNullOrEmpty(currencyCode))
                {
                    query = query.Where(h => h.CurrencyCode == currencyCode);
                }

                var today = DateTime.Today;
                var totalTransactions = await query.CountAsync();
                var todayTransactions = await query.CountAsync(h => h.TransactionDate.Date == today);

                // Get current balance from latest record for each currency
                var latestBalances = await _context.CurrencyPoolHistory
                    .Where(h => string.IsNullOrEmpty(currencyCode) || h.CurrencyCode == currencyCode)
                    .GroupBy(h => h.CurrencyCode)
                    .Select(g => new
                    {
                        Currency = g.Key,
                        Balance = g.OrderByDescending(h => h.TransactionDate)
                                  .ThenByDescending(h => h.Id)
                                  .First().BalanceAfter
                    })
                    .ToListAsync();

                return new PoolSummary
                {
                    TotalTransactions = totalTransactions,
                    TodayTransactions = todayTransactions,
                    CurrencyBalances = latestBalances.ToDictionary(b => b.Currency, b => b.Balance),
                    LastUpdateTime = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading pool summary: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Pool Timeline Item for display
    /// آیتم جدول زمانی پول برای نمایش
    /// </summary>
    public class PoolTimelineItem
    {
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public int? ReferenceId { get; set; }
        public bool CanNavigate { get; set; }
    }

    /// <summary>
    /// Pool Summary Statistics
    /// آمار خلاصه پول
    /// </summary>
    public class PoolSummary
    {
        public int TotalTransactions { get; set; }
        public int TodayTransactions { get; set; }
        public Dictionary<string, decimal> CurrencyBalances { get; set; } = new();
        public DateTime LastUpdateTime { get; set; }
    }
}
