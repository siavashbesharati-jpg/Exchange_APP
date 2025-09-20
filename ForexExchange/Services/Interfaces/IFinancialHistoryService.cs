namespace ForexExchange.Services.Interfaces
{
    /// <summary>
    /// Base interface for all financial history services
    /// واسط پایه برای همه سرویس‌های تاریخچه مالی
    /// </summary>
    /// <typeparam name="TTimelineItem">Type of timeline item</typeparam>
    /// <typeparam name="TSummary">Type of summary statistics</typeparam>
    public interface IFinancialHistoryService<TTimelineItem, TSummary>
        where TTimelineItem : class, ITimelineItem
        where TSummary : class, ISummaryStatistics
    {
        /// <summary>
        /// Get timeline with date filtering
        /// دریافت جدول زمانی با فیلتر تاریخ
        /// </summary>
        Task<List<TTimelineItem>> GetTimelineAsync(DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Get summary statistics
        /// دریافت آمار خلاصه
        /// </summary>
        Task<TSummary> GetSummaryAsync();
    }
}