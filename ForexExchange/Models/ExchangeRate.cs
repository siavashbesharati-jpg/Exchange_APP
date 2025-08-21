using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    /// <summary>
    /// Exchange Rate Model for Cross-Currency Trading
    /// مدل نرخ ارز برای تجارت متقابل ارزها
    /// </summary>
    public class ExchangeRate
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Base currency ID (what you're selling/from)
        /// شناسه ارز پایه (آنچه می‌فروشید/از)
        /// </summary>
        [Required]
        [Display(Name = "From Currency - از ارز")]
        public int FromCurrencyId { get; set; }
        
        /// <summary>
        /// Quote currency ID (what you're buying/to)
        /// شناسه ارز نقل قول (آنچه می‌خرید/به)
        /// </summary>
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
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Buy Rate - نرخ خرید")]
        public decimal BuyRate { get; set; }  // نرخ خرید
        
        [Required]
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Sell Rate - نرخ فروش")]
        public decimal SellRate { get; set; } // نرخ فروش
        
        [Display(Name = "Updated At - تاریخ بروزرسانی")]
        public DateTime UpdatedAt { get; set; }
        
        [Required]
        [StringLength(50)]
        [Display(Name = "Updated By - بروزرسانی توسط")]
        public string UpdatedBy { get; set; } = "System";
        
        [Display(Name = "Is Active - فعال")]
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Currency pair identifier (e.g., "USD/EUR", "AED/TRY")
        /// شناسه جفت ارز
        /// </summary>
        [Display(Name = "Currency Pair - جفت ارز")]
        public string CurrencyPair => $"{FromCurrency?.Code}/{ToCurrency?.Code}";
        
        /// <summary>
        /// Check if this is a cross-currency rate (not involving IRR)
        /// بررسی آیا این نرخ متقابل است (شامل ریال نمی‌شود)
        /// </summary>
        public bool IsCrossCurrency => FromCurrency?.Code != "IRR" && ToCurrency?.Code != "IRR";
        
        /// <summary>
        /// Calculate spread (difference between sell and buy rate)
        /// محاسبه اسپرد (تفاوت بین نرخ فروش و خرید)
        /// </summary>
        public decimal Spread => SellRate - BuyRate;
        
        /// <summary>
        /// Calculate spread percentage
        /// محاسبه درصد اسپرد
        /// </summary>
        public decimal SpreadPercentage => BuyRate > 0 ? (Spread / BuyRate) * 100 : 0;
        
        /// <summary>
        /// Get mid-market rate (average of buy and sell)
        /// دریافت نرخ میانگین بازار
        /// </summary>
        public decimal MidRate => (BuyRate + SellRate) / 2;
    }
}
