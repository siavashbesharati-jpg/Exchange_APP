namespace ForexExchange.Services.Interfaces
{
    /// <summary>
    /// Common interface for summary statistics
    /// واسط مشترک برای آمار خلاصه
    /// </summary>
    public interface ISummaryStatistics
    {
        /// <summary>
        /// Total number of transactions
        /// تعداد کل تراکنش‌ها
        /// </summary>
        int TotalTransactions { get; set; }

        /// <summary>
        /// Number of transactions today
        /// تعداد تراکنش‌های امروز
        /// </summary>
        int TodayTransactions { get; set; }

        /// <summary>
        /// Last update time
        /// زمان آخرین بروزرسانی
        /// </summary>
        DateTime LastUpdateTime { get; set; }
    }
}