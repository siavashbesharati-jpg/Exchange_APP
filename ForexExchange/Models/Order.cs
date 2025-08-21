using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    public enum CurrencyType
    {
        Toman = 0,     // تومان
        USD = 1,       // دلار آمریکا
        EUR = 2,       // یورو
        AED = 3,       // درهم امارات
        OMR = 4,       // ریال عمان
        TRY = 5        // لیر ترکیه
    }
    
    public enum OrderType
    {
        Buy = 0,   // خرید
        Sell = 1   // فروش
    }
    
    public enum OrderStatus
    {
        Open = 0,          // باز
        Matched = 1,       // مچ شده
        PartiallyFilled = 2, // نیمه تکمیل
        Completed = 3,     // تکمیل شده
        Cancelled = 4      // لغو شده
    }
    
    public class Order
    {
        public int Id { get; set; }
        
        [Required]
        public int CustomerId { get; set; }
        
        [Required]
        public OrderType OrderType { get; set; }
        
        [Required]
        [Display(Name = "From Currency - از ارز")]
        public CurrencyType FromCurrency { get; set; }
        
        [Required]
        [Display(Name = "To Currency - به ارز")]
        public CurrencyType ToCurrency { get; set; }
        
        /// <summary>
        /// Legacy field for backward compatibility - now maps to FromCurrency
        /// فیلد قدیمی برای سازگاری - اکنون به FromCurrency نگاشت می‌شود
        /// </summary>
        [Required]
        public CurrencyType Currency 
        { 
            get => FromCurrency; 
            set => FromCurrency = value; 
        }
        
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
        /// Legacy field for backward compatibility - now calculated based on ToCurrency
        /// فیلد قدیمی برای سازگاری - اکنون بر اساس ToCurrency محاسبه می‌شود
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalInToman 
        { 
            get => ToCurrency == CurrencyType.Toman ? TotalAmount : 0; 
            set { if (ToCurrency == CurrencyType.Toman) TotalAmount = value; }
        }
        
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Filled Amount - مقدار تکمیل شده")]
        public decimal FilledAmount { get; set; } = 0;
        
        [Required]
        [Display(Name = "Status - وضعیت")]
        public OrderStatus Status { get; set; } = OrderStatus.Open;
        
        [Display(Name = "Created At - تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [Display(Name = "Updated At - تاریخ بروزرسانی")]
        public DateTime? UpdatedAt { get; set; }
        
        [StringLength(500)]
        [Display(Name = "Notes - یادداشت‌ها")]
        public string? Notes { get; set; }
        
        /// <summary>
        /// Cross-currency pair identifier (e.g., "USD/EUR", "AED/TRY")
        /// شناسه جفت ارز متقابل
        /// </summary>
        [Display(Name = "Currency Pair - جفت ارز")]
        public string CurrencyPair => $"{FromCurrency}/{ToCurrency}";
        
        /// <summary>
        /// Calculate remaining amount to be filled
        /// محاسبه مقدار باقی‌مانده برای تکمیل
        /// </summary>
        public decimal RemainingAmount => Amount - FilledAmount;
        
        /// <summary>
        /// Check if order is cross-currency (not involving Toman)
        /// بررسی آیا سفارش متقابل است (شامل تومان نمی‌شود)
        /// </summary>
        public bool IsCrossCurrency => FromCurrency != CurrencyType.Toman && ToCurrency != CurrencyType.Toman;
        
        // Navigation properties
        public Customer Customer { get; set; } = null!;
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
    }
}
