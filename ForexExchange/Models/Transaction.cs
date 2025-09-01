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
        [Display(Name = "From Currency - از ارز")]
        public int FromCurrencyId { get; set; }
        
        [Required]
        [Display(Name = "To Currency - به ارز")]
        public int ToCurrencyId { get; set; }
        
        /// <summary>
        /// Navigation property for From Currency
        /// خاصیت ناوبری برای ارز مبدأ
        /// </summary>
        public Currency FromCurrency { get; set; } = null!;
        
        /// <summary>
        /// Navigation property for To Currency
        /// خاصیت ناوبری برای ارز مقصد
        /// </summary>
        public Currency ToCurrency { get; set; } = null!;
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Amount - مقدار")]
        public decimal Amount { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Exchange Rate - نرخ تبدیل")]
        public decimal Rate { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Amount - مقدار کل")]
        public decimal TotalAmount { get; set; }
        
        /// <summary>
        /// Legacy field for backward compatibility
        /// فیلد قدیمی برای سازگاری
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalInToman 
        { 
            get => ToCurrency?.Code == "IRR" ? TotalAmount : 0; 
            set { if (ToCurrency?.Code == "IRR") TotalAmount = value; }
        }
        
        [Required]
        [Display(Name = "Status - وضعیت")]
        public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

        [Display(Name = "Created At - تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [Display(Name = "Completed At - تاریخ تکمیل")]
        public DateTime? CompletedAt { get; set; }
        
        [Display(Name = "Buyer Bank Account - حساب بانکی خریدار")]
        public int? BuyerBankAccountId { get; set; }
        
        [Display(Name = "Seller Bank Account - حساب بانکی فروشنده")]
        public int? SellerBankAccountId { get; set; }
        
        [StringLength(100)]
        [Display(Name = "Buyer Bank Account (Legacy) - حساب بانکی خریدار (قدیمی)")]
        public string? BuyerBankAccount { get; set; }
        
        [StringLength(100)]
        [Display(Name = "Seller Bank Account (Legacy) - حساب بانکی فروشنده (قدیمی)")]
        public string? SellerBankAccount { get; set; }
        
        [StringLength(500)]
        [Display(Name = "Notes - یادداشت‌ها")]
        public string? Notes { get; set; }
        
        /// <summary>
        /// Cross-currency pair identifier (e.g., "USD/EUR", "AED/TRY")
        /// شناسه جفت ارز متقابل
        /// </summary>
        [Display(Name = "Currency Pair - جفت ارز")]
        public string CurrencyPair => $"{FromCurrency?.Code}/{ToCurrency?.Code}";
        
        /// <summary>
        /// Check if transaction is cross-currency (not involving IRR)
        /// بررسی آیا تراکنش متقابل است (شامل ریال نمی‌شود)
        /// </summary>
        public bool IsCrossCurrency => FromCurrency?.Code != "IRR" && ToCurrency?.Code != "IRR";
        
        // Navigation properties
        public Order BuyOrder { get; set; } = null!;
        public Order SellOrder { get; set; } = null!;
        public Customer BuyerCustomer { get; set; } = null!;
        public Customer SellerCustomer { get; set; } = null!;
        public BankAccount? BuyerBankAccountNavigation { get; set; }
        public BankAccount? SellerBankAccountNavigation { get; set; }
        public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
    }
}
