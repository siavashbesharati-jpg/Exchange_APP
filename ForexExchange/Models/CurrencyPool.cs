using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    /// <summary>
    /// Currency Pool Model - مدل استخر ارزی
    /// Tracks real-time balance of each currency in the exchange's pool
    /// ردیابی موجودی لحظه‌ای هر ارز در استخر صرافی
    /// </summary>
    public class CurrencyPool
    {
        /// <summary>
        /// Unique identifier for the pool record
        /// شناسه یکتای رکورد استخر
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Currency code (USD, EUR, GBP, etc.)
        /// کد ارز (دلار، یورو، پوند و غیره)
        /// </summary>
        [Required]
        [StringLength(3, MinimumLength = 3)]
        [Display(Name = "Currency - ارز")]
        public string Currency { get; set; } = string.Empty;

        /// <summary>
        /// Current balance in the pool for this currency
        /// موجودی فعلی در استخر برای این ارز
        /// Positive: Exchange has surplus (can sell)
        /// Negative: Exchange has deficit (needs to buy)
        /// مثبت: صرافی مازاد دارد (می‌تواند بفروشد)
        /// منفی: صرافی کسری دارد (باید بخرد)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,8)")]
        [Display(Name = "Balance - موجودی")]
        public decimal Balance { get; set; }

        /// <summary>
        /// Total amount bought by the exchange (cumulative)
        /// مجموع مقدار خریداری شده توسط صرافی (تجمعی)
        /// </summary>
        [Column(TypeName = "decimal(18,8)")]
        [Display(Name = "Total Bought - مجموع خرید")]
        public decimal TotalBought { get; set; }

        /// <summary>
        /// Total amount sold by the exchange (cumulative)
        /// مجموع مقدار فروخته شده توسط صرافی (تجمعی)
        /// </summary>
        [Column(TypeName = "decimal(18,8)")]
        [Display(Name = "Total Sold - مجموع فروش")]
        public decimal TotalSold { get; set; }

        /// <summary>
        /// Average buy rate for current balance
        /// میانگین نرخ خرید برای موجودی فعلی
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Avg Buy Rate - میانگین نرخ خرید")]
        public decimal? AverageBuyRate { get; set; }

        /// <summary>
        /// Average sell rate for sold amounts
        /// میانگین نرخ فروش برای مقادیر فروخته شده
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Avg Sell Rate - میانگین نرخ فروش")]
        public decimal? AverageSellRate { get; set; }

        /// <summary>
        /// Last time this pool was updated
        /// آخرین زمان بروزرسانی این استخر
        /// </summary>
        [Required]
        [Display(Name = "Last Updated - آخرین بروزرسانی")]
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Risk level based on balance and thresholds
        /// سطح ریسک بر اساس موجودی و آستانه‌ها
        /// </summary>
        [Display(Name = "Risk Level - سطح ریسک")]
        public PoolRiskLevel RiskLevel { get; set; }

        /// <summary>
        /// Notes or comments about this pool
        /// یادداشت‌ها یا نظرات در مورد این استخر
        /// </summary>
        [StringLength(500)]
        [Display(Name = "Notes - یادداشت‌ها")]
        public string? Notes { get; set; }

        /// <summary>
        /// Whether this currency pool is active
        /// آیا این استخر ارزی فعال است
        /// </summary>
        [Display(Name = "Is Active - فعال")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Calculate net profit/loss for this currency
        /// محاسبه سود/زیان خالص برای این ارز
        /// </summary>
        /// <returns>Net profit (positive) or loss (negative)</returns>
        public decimal CalculateNetProfitLoss()
        {
            if (AverageBuyRate == null || AverageSellRate == null)
                return 0;

            decimal totalBuyValue = TotalBought * AverageBuyRate.Value;
            decimal totalSellValue = TotalSold * AverageSellRate.Value;
            
            return totalSellValue - totalBuyValue;
        }

        /// <summary>
        /// Calculate current position value at given rate
        /// محاسبه ارزش موقعیت فعلی در نرخ داده شده
        /// </summary>
        /// <param name="currentRate">Current market rate</param>
        /// <returns>Current position value in Toman</returns>
        public decimal CalculateCurrentPositionValue(decimal currentRate)
        {
            return Balance * currentRate;
        }

        /// <summary>
        /// Update risk level based on balance and thresholds
        /// بروزرسانی سطح ریسک بر اساس موجودی و آستانه‌ها
        /// </summary>
        /// <param name="lowThreshold">Low risk threshold</param>
        /// <param name="highThreshold">High risk threshold</param>
        public void UpdateRiskLevel(decimal lowThreshold, decimal highThreshold)
        {
            decimal absBalance = Math.Abs(Balance);
            
            if (absBalance <= lowThreshold)
                RiskLevel = PoolRiskLevel.Low;
            else if (absBalance <= highThreshold)
                RiskLevel = PoolRiskLevel.Medium;
            else
                RiskLevel = PoolRiskLevel.High;
        }
    }

    /// <summary>
    /// Risk levels for currency pools
    /// سطوح ریسک برای استخرهای ارزی
    /// </summary>
    public enum PoolRiskLevel
    {
        /// <summary>
        /// Low risk - balanced position
        /// ریسک کم - موقعیت متعادل
        /// </summary>
        [Display(Name = "Low - کم")]
        Low = 1,

        /// <summary>
        /// Medium risk - moderate imbalance
        /// ریسک متوسط - عدم تعادل متوسط
        /// </summary>
        [Display(Name = "Medium - متوسط")]
        Medium = 2,

        /// <summary>
        /// High risk - significant imbalance
        /// ریسک بالا - عدم تعادل قابل توجه
        /// </summary>
        [Display(Name = "High - بالا")]
        High = 3,

        /// <summary>
        /// Critical risk - requires immediate attention
        /// ریسک بحرانی - نیاز به توجه فوری
        /// </summary>
        [Display(Name = "Critical - بحرانی")]
        Critical = 4
    }
}
