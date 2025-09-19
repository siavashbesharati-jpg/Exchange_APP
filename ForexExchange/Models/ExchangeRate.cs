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
        [Column(TypeName = "decimal(18,8)")]
        [Display(Name = "Rate - نرخ ارز")]
        public decimal Rate { get; set; }  // نرخ ارز

        /// <summary>
        /// Average buy rate for this currency pair (weighted average of buy orders)
        /// میانگین نرخ خرید برای این جفت ارز (میانگین وزنی معاملات  خرید)
        /// </summary>
        [Column(TypeName = "decimal(18,8)")]
        [Display(Name = "Average Buy Rate - میانگین نرخ خرید")]
        public decimal? AverageBuyRate { get; set; }

        /// <summary>
        /// Average sell rate for this currency pair (weighted average of sell orders)
        /// میانگین نرخ فروش برای این جفت ارز (میانگین وزنی معاملات  فروش)
        /// </summary>
        [Column(TypeName = "decimal(18,8)")]
        [Display(Name = "Average Sell Rate - میانگین نرخ فروش")]
        public decimal? AverageSellRate { get; set; }

        /// <summary>
        /// Total volume of buy orders for this pair
        /// حجم کل معاملات  خرید برای این جفت ارز
        /// </summary>
        [Column(TypeName = "decimal(18,8)")]
        [Display(Name = "Total Buy Volume - حجم کل خرید")]
        public decimal TotalBuyVolume { get; set; }

        /// <summary>
        /// Total volume of sell orders for this pair
        /// حجم کل معاملات  فروش برای این جفت ارز
        /// </summary>
        [Column(TypeName = "decimal(18,8)")]
        [Display(Name = "Total Sell Volume - حجم کل فروش")]
        public decimal TotalSellVolume { get; set; }

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

        // Spread, SpreadPercentage, and MidRate removed (single rate system)
    }
}
