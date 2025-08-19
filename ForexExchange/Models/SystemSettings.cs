using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class SystemSettings
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string SettingKey { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string SettingValue { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string DataType { get; set; } = "string"; // string, decimal, int, bool

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string UpdatedBy { get; set; } = "System";
    }

    // Helper class for strongly typed settings access
    public static class SettingKeys
    {
        public const string CommissionRate = "COMMISSION_RATE";
        public const string ExchangeFeeRate = "EXCHANGE_FEE_RATE";
        public const string MinTransactionAmount = "MIN_TRANSACTION_AMOUNT";
        public const string MaxTransactionAmount = "MAX_TRANSACTION_AMOUNT";
        public const string DailyTransactionLimit = "DAILY_TRANSACTION_LIMIT";
        public const string SystemMaintenance = "SYSTEM_MAINTENANCE";
        public const string DefaultCurrency = "DEFAULT_CURRENCY";
        public const string RateUpdateInterval = "RATE_UPDATE_INTERVAL";
        public const string NotificationEnabled = "NOTIFICATION_ENABLED";
        public const string BackupEnabled = "BACKUP_ENABLED";
    }

    // Settings view model for management interface
    public class SystemSettingsViewModel
    {
        [Display(Name = "نرخ کمیسیون (%)")]
        [Range(0, 100, ErrorMessage = "نرخ کمیسیون باید بین 0 تا 100 درصد باشد")]
        public decimal CommissionRate { get; set; }

        [Display(Name = "کارمزد تبدیل ارز (%)")]
        [Range(0, 100, ErrorMessage = "کارمزد تبدیل باید بین 0 تا 100 درصد باشد")]
        public decimal ExchangeFeeRate { get; set; }

        [Display(Name = "حداقل مبلغ تراکنش (تومان)")]
        [Range(1000, 1000000000, ErrorMessage = "حداقل مبلغ باید بین 1,000 تا 1,000,000,000 تومان باشد")]
        public decimal MinTransactionAmount { get; set; }

        [Display(Name = "حداکثر مبلغ تراکنش (تومان)")]
        [Range(1000, 10000000000, ErrorMessage = "حداکثر مبلغ باید بین 1,000 تا 10,000,000,000 تومان باشد")]
        public decimal MaxTransactionAmount { get; set; }

        [Display(Name = "محدودیت تراکنش روزانه (تومان)")]
        [Range(10000, 100000000000, ErrorMessage = "محدودیت روزانه باید بین 10,000 تا 100,000,000,000 تومان باشد")]
        public decimal DailyTransactionLimit { get; set; }

        [Display(Name = "حالت تعمیرات سیستم")]
        public bool SystemMaintenance { get; set; }

        [Display(Name = "ارز پیش‌فرض")]
        public CurrencyType DefaultCurrency { get; set; }

        [Display(Name = "بازه بروزرسانی نرخ (دقیقه)")]
        [Range(1, 1440, ErrorMessage = "بازه بروزرسانی باید بین 1 تا 1440 دقیقه باشد")]
        public int RateUpdateInterval { get; set; }

        [Display(Name = "فعال‌سازی اعلان‌ها")]
        public bool NotificationEnabled { get; set; }

        [Display(Name = "فعال‌سازی پشتیبان‌گیری خودکار")]
        public bool BackupEnabled { get; set; }
    }
}
