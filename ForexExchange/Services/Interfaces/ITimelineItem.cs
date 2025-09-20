namespace ForexExchange.Services.Interfaces
{
    /// <summary>
    /// Common interface for all timeline item types
    /// واسط مشترک برای همه انواع آیتم‌های جدول زمانی
    /// </summary>
    public interface ITimelineItem
    {
        /// <summary>
        /// Date in yyyy/MM/dd format
        /// تاریخ در فرمت yyyy/MM/dd
        /// </summary>
        string Date { get; set; }

        /// <summary>
        /// Time in HH:mm format
        /// زمان در فرمت HH:mm
        /// </summary>
        string Time { get; set; }

        /// <summary>
        /// Type of transaction
        /// نوع تراکنش
        /// </summary>
        string TransactionType { get; set; }

        /// <summary>
        /// Description of the transaction
        /// توضیحات تراکنش
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// Transaction amount
        /// مبلغ تراکنش
        /// </summary>
        decimal Amount { get; set; }

        /// <summary>
        /// Balance after transaction
        /// موجودی پس از تراکنش
        /// </summary>
        decimal Balance { get; set; }

        /// <summary>
        /// Reference ID for navigation
        /// شناسه مرجع برای ناوبری
        /// </summary>
        int? ReferenceId { get; set; }

        /// <summary>
        /// Whether the item can be navigated to
        /// آیا آیتم قابل ناوبری است
        /// </summary>
        bool CanNavigate { get; set; }
    }
}