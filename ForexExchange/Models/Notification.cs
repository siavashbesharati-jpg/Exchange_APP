using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class Notification
    {
        public int Id { get; set; }
        
        [Required]
        public int CustomerId { get; set; }
        
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";
        
        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = "";
        
        public NotificationType Type { get; set; }
        
        public int? RelatedEntityId { get; set; }
        
        public NotificationPriority Priority { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public bool IsRead { get; set; }
        
        public DateTime? ReadAt { get; set; }

        // Navigation property
        public Customer Customer { get; set; } = null!;
    }

    public enum NotificationType
    {
        OrderCreated,
        OrderMatched,
        TransactionStatusChanged,
        AccountingDocumentUploaded,
        AccountingDocumentVerified,
        SystemAlert,
        PaymentReminder
    }

    public enum NotificationPriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}
