using ForexExchange.Services.Interfaces;

namespace ForexExchange.Services
{
    /// <summary>
    /// Adapter for CustomerTransactionHistory to implement ITimelineItem
    /// آداپتور برای CustomerTransactionHistory برای پیاده‌سازی ITimelineItem
    /// </summary>
    public class CustomerTimelineItemAdapter : ITimelineItem
    {
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public int? ReferenceId { get; set; }
        public bool CanNavigate { get; set; }

        // Customer specific properties
        public int CustomerId { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string? TransactionNumber { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Simple summary adapter for customer statistics
    /// آداپتور ساده برای آمار مشتری
    /// </summary>
    public class CustomerSummaryAdapter : ISummaryStatistics
    {
        public int TotalTransactions { get; set; }
        public int TodayTransactions { get; set; }
        public DateTime LastUpdateTime { get; set; }

        // Additional customer-specific data
        public Dictionary<string, object> ExtendedData { get; set; } = new();
    }
}