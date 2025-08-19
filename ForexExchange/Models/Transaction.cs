using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    public enum TransactionStatus
    {
        Pending = 0,       // در انتظار
        PaymentUploaded = 1, // رسید پرداخت آپلود شده
        ReceiptConfirmed = 2, // رسید تأیید شده
        Completed = 3,     // تکمیل شده
        Failed = 4         // ناموفق
    }
    
    public class Transaction
    {
        public int Id { get; set; }
        
        [Required]
        public int BuyOrderId { get; set; }
        
        [Required]
        public int SellOrderId { get; set; }
        
        [Required]
        public int BuyerCustomerId { get; set; }
        
        [Required]
        public int SellerCustomerId { get; set; }
        
        [Required]
        public CurrencyType Currency { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Rate { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalInToman { get; set; }
        
        [Required]
        public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        
        [StringLength(100)]
        public string? BuyerBankAccount { get; set; }
        
        [StringLength(100)]
        public string? SellerBankAccount { get; set; }
        
        [StringLength(500)]
        public string? Notes { get; set; }
        
        // Navigation properties
        public Order BuyOrder { get; set; } = null!;
        public Order SellOrder { get; set; } = null!;
        public Customer BuyerCustomer { get; set; } = null!;
        public Customer SellerCustomer { get; set; } = null!;
        public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
    }
}
