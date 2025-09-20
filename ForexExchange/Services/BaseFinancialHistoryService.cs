using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services.Interfaces;

namespace ForexExchange.Services
{
    /// <summary>
    /// Base class for all financial history services providing common functionality
    /// کلاس پایه برای همه سرویس‌های تاریخچه مالی با قابلیت‌های مشترک
    /// </summary>
    /// <typeparam name="TTimelineItem">Type of timeline item that implements ITimelineItem</typeparam>
    /// <typeparam name="TSummary">Type of summary statistics that implements ISummaryStatistics</typeparam>
    public abstract class BaseFinancialHistoryService<TTimelineItem, TSummary>
        where TTimelineItem : class, ITimelineItem
        where TSummary : class, ISummaryStatistics
    {
        protected readonly ForexDbContext _context;

        protected BaseFinancialHistoryService(ForexDbContext context)
        {
            _context = context;
        }

        #region Common Helper Methods

        /// <summary>
        /// Formats DateTime to Persian date string
        /// تبدیل تاریخ به رشته تاریخ فارسی
        /// </summary>
        protected string FormatPersianDate(DateTime date)
        {
            var persianCalendar = new System.Globalization.PersianCalendar();
            var year = persianCalendar.GetYear(date);
            var month = persianCalendar.GetMonth(date);
            var day = persianCalendar.GetDayOfMonth(date);
            return $"{year:0000}/{month:00}/{day:00}";
        }

        /// <summary>
        /// Formats DateTime to time string
        /// تبدیل زمان به رشته زمان
        /// </summary>
        protected string FormatTime(DateTime dateTime)
        {
            return dateTime.ToString("HH:mm:ss");
        }

        /// <summary>
        /// Validates and normalizes date range parameters
        /// اعتبارسنجی و عادی‌سازی پارامترهای بازه تاریخ
        /// </summary>
        protected (DateTime fromDate, DateTime toDate) ValidateDateRange(DateTime? fromDate, DateTime? toDate)
        {
            var actualFromDate = fromDate ?? DateTime.MinValue;
            var actualToDate = toDate ?? DateTime.MaxValue;

            // Ensure toDate includes the entire day
            if (toDate.HasValue && toDate.Value.TimeOfDay == TimeSpan.Zero)
            {
                actualToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
            }

            return (actualFromDate, actualToDate);
        }

        /// <summary>
        /// Applies common date filtering to any IQueryable
        /// اعمال فیلتر تاریخ مشترک به هر IQueryable
        /// </summary>
        protected IQueryable<T> ApplyDateFilter<T>(IQueryable<T> query, DateTime fromDate, DateTime toDate, 
            Func<T, DateTime> dateSelector)
        {
            return query.Where(item => dateSelector(item) >= fromDate && dateSelector(item) <= toDate);
        }

        /// <summary>
        /// Formats currency amount with proper formatting
        /// قالب‌بندی مبلغ ارزی با فرمت مناسب
        /// </summary>
        protected string FormatCurrencyAmount(decimal amount)
        {
            return amount.ToString("N0");
        }

        #endregion

        #region Abstract Methods - Must be implemented by derived classes

        /// <summary>
        /// Gets timeline items for the specific entity type
        /// دریافت آیتم‌های جدول زمانی برای نوع موجودیت خاص
        /// </summary>
        protected abstract Task<List<TTimelineItem>> GetTimelineItemsAsync(DateTime fromDate, DateTime toDate, object? filter = null);

        /// <summary>
        /// Gets summary statistics for the specific entity type
        /// دریافت آمار خلاصه برای نوع موجودیت خاص
        /// </summary>
        protected abstract Task<TSummary> GetSummaryStatisticsAsync(object? filter = null);

        /// <summary>
        /// Generates transaction description for the specific entity type
        /// تولید توضیحات تراکنش برای نوع موجودیت خاص
        /// </summary>
        protected abstract string GenerateTransactionDescription(object transactionRecord);

        #endregion

        #region Virtual Methods - Can be overridden by derived classes

        /// <summary>
        /// Orders timeline items by date (can be overridden for custom sorting)
        /// مرتب‌سازی آیتم‌های جدول زمانی بر اساس تاریخ
        /// </summary>
        protected virtual List<TTimelineItem> OrderTimelineItems(List<TTimelineItem> items)
        {
            return items.OrderByDescending(item => DateTime.Parse($"{item.Date} {item.Time}")).ToList();
        }

        /// <summary>
        /// Validates timeline item before adding to results
        /// اعتبارسنجی آیتم جدول زمانی قبل از افزودن به نتایج
        /// </summary>
        protected virtual bool IsValidTimelineItem(TTimelineItem item)
        {
            return !string.IsNullOrEmpty(item.Date) && 
                   !string.IsNullOrEmpty(item.Time) && 
                   !string.IsNullOrEmpty(item.TransactionType);
        }

        /// <summary>
        /// Post-processes timeline items (can be overridden for custom processing)
        /// پردازش نهایی آیتم‌های جدول زمانی
        /// </summary>
        protected virtual List<TTimelineItem> PostProcessTimelineItems(List<TTimelineItem> items)
        {
            return items.Where(IsValidTimelineItem).ToList();
        }

        #endregion

        #region Common Public Interface

        /// <summary>
        /// Gets timeline with date filtering - common interface for all services
        /// دریافت جدول زمانی با فیلتر تاریخ - رابط مشترک برای همه سرویس‌ها
        /// </summary>
        public async Task<List<TTimelineItem>> GetTimelineAsync(DateTime? fromDate = null, DateTime? toDate = null, object? filter = null)
        {
            var (validFromDate, validToDate) = ValidateDateRange(fromDate, toDate);
            var items = await GetTimelineItemsAsync(validFromDate, validToDate, filter);
            var orderedItems = OrderTimelineItems(items);
            return PostProcessTimelineItems(orderedItems);
        }

        /// <summary>
        /// Gets summary statistics - common interface for all services
        /// دریافت آمار خلاصه - رابط مشترک برای همه سرویس‌ها
        /// </summary>
        public async Task<TSummary> GetSummaryAsync(object? filter = null)
        {
            return await GetSummaryStatisticsAsync(filter);
        }

        #endregion

        #region Common Statistics Helpers

        /// <summary>
        /// Calculates common statistical values from timeline items
        /// محاسبه مقادیر آماری مشترک از آیتم‌های جدول زمانی
        /// </summary>
        protected (int totalTransactions, int todayTransactions, DateTime lastUpdateTime) CalculateCommonStatistics(
            List<TTimelineItem> timelineItems)
        {
            var today = DateTime.Today;
            var totalTransactions = timelineItems.Count;
            var todayTransactions = timelineItems.Count(item => 
            {
                if (DateTime.TryParse(item.Date, out var itemDate))
                {
                    return itemDate.Date == today;
                }
                return false;
            });

            return (totalTransactions, todayTransactions, DateTime.Now);
        }

        /// <summary>
        /// Groups timeline items by date for analysis
        /// گروه‌بندی آیتم‌های جدول زمانی بر اساس تاریخ برای تجزیه و تحلیل
        /// </summary>
        protected Dictionary<string, List<TTimelineItem>> GroupItemsByDate(List<TTimelineItem> items)
        {
            return items.GroupBy(item => item.Date)
                       .ToDictionary(group => group.Key, group => group.ToList());
        }

        /// <summary>
        /// Calculates balance trends from timeline items
        /// محاسبه روند موجودی از آیتم‌های جدول زمانی
        /// </summary>
        protected (decimal highestBalance, decimal lowestBalance) CalculateBalanceTrends(List<TTimelineItem> items)
        {
            if (!items.Any())
                return (0, 0);

            var highestBalance = items.Max(item => item.Balance);
            var lowestBalance = items.Min(item => item.Balance);
            
            return (highestBalance, lowestBalance);
        }

        #endregion
    }
}