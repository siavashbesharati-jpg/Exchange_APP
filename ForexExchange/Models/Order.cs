using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    
   
   
    
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
        
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Filled Amount - مقدار پر شده")]
        public decimal FilledAmount { get; set; }
        
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
        
       
        
        // Navigation properties
        public Customer Customer { get; set; } = null!;
        public Currency FromCurrency { get; set; } = null!;
        public Currency ToCurrency { get; set; } = null!;
    }
}
