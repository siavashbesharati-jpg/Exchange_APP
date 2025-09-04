using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    
   
    public enum OrderStatus
    {
        Open = 0,          // باز
        Matched = 1,       // مچ شده
        Completed = 3,     // تکمیل شده
        Cancelled = 4      // لغو شده
    }
    
    public class Order
    {
        public int Id { get; set; }
        
        [Required]
        public int CustomerId { get; set; }
        
       
        
        [Required]
        [Display(Name = "From Currency - از ارز")]
        public int FromCurrencyId { get; set; }
        
        [Required]
        [Display(Name = "To Currency - به ارز")]
        public int ToCurrencyId { get; set; }
        
       
        
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
        /// Legacy field for backward compatibility - now calculated based on ToCurrencyId
        /// فیلد قدیمی برای سازگاری - اکنون بر اساس ToCurrencyId محاسبه می‌شود
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalInToman 
        { 
            get => ToCurrency?.Code == "IRR" ? TotalAmount : 0; 
            set { if (ToCurrency?.Code == "IRR") TotalAmount = value; }
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
        public string CurrencyPair => $"{FromCurrency?.Code}/{ToCurrency?.Code}";
        
        /// <summary>
        /// Calculate remaining amount to be filled
        /// محاسبه مقدار باقی‌مانده برای تکمیل
        /// </summary>
        public decimal RemainingAmount => Amount - FilledAmount;
        
        /// <summary>
        /// Check if order is cross-currency (not involving IRR/Toman)
        /// بررسی آیا سفارش متقابل است (شامل تومان نمی‌شود)
        /// </summary>
        public bool IsCrossCurrency => FromCurrency?.Code != "IRR" && ToCurrency?.Code != "IRR";
        
        // Navigation properties
        public Customer Customer { get; set; } = null!;
        public Currency FromCurrency { get; set; } = null!;
        public Currency ToCurrency { get; set; } = null!;
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
    }
}
