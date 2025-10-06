using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class SystemSettings
    {
        [Key]
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

        // Website Branding Settings
        public const string WebsiteName = "WEBSITE_NAME";
        public const string WebsiteLogoPath = "WEBSITE_LOGO_PATH";
        public const string WebsiteLogoBase64 = "WEBSITE_LOGO_BASE64";
        public const string WebsiteLogoMimeType = "WEBSITE_LOGO_MIME_TYPE";
        public const string CompanyName = "COMPANY_NAME";
        public const string CompanyWebsite = "COMPANY_WEBSITE";
        
        // Application Mode Settings
        public const string IsDemoMode = "IS_DEMO_MODE";
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

        [Display(Name = "کد ارز پیش‌فرض")]
        [StringLength(3)]
        public string DefaultCurrencyCode { get; set; } = "USD";

        [Display(Name = "بازه بروزرسانی نرخ (دقیقه)")]
        [Range(1, 1440, ErrorMessage = "بازه بروزرسانی باید بین 1 تا 1440 دقیقه باشد")]
        public int RateUpdateInterval { get; set; }

        [Display(Name = "فعال‌سازی اعلان‌ها")]
        public bool NotificationEnabled { get; set; }

        [Display(Name = "فعال‌سازی پشتیبان‌گیری خودکار")]
        public bool BackupEnabled { get; set; }

        [Display(Name = "حالت نمایشی (دمو)")]
        public bool IsDemoMode { get; set; }

        // Website Branding Settings
        [Display(Name = "نام وب‌سایت")]
        [StringLength(100, ErrorMessage = "نام وب‌سایت نمی‌تواند بیش از 100 کاراکتر باشد")]
        public string WebsiteName { get; set; } = "سامانه معاملات اکسورا";

        [Display(Name = "نام شرکت")]
        [StringLength(100, ErrorMessage = "نام شرکت نمی‌تواند بیش از 100 کاراکتر باشد")]
        public string CompanyName { get; set; } = "گروه اکسورا";

        [Display(Name = "وب‌سایت شرکت")]
        [StringLength(200, ErrorMessage = "آدرس وب‌سایت نمی‌تواند بیش از 200 کاراکتر باشد")]
        [Url(ErrorMessage = "لطفاً آدرس وب‌سایت معتبر وارد کنید")]
        public string CompanyWebsite { get; set; } = "https://Exsora.iranexpedia.ir";

        [Display(Name = "مسیر لوگو")]
        [StringLength(500, ErrorMessage = "مسیر لوگو نمی‌تواند بیش از 500 کاراکتر باشد")]
        public string? LogoPath { get; set; }

        [Display(Name = "لوگو (Base64)")]
        public string? LogoBase64 { get; set; }

        [Display(Name = "نوع فایل لوگو")]
        [StringLength(50, ErrorMessage = "نوع فایل نمی‌تواند بیش از 50 کاراکتر باشد")]
        public string? LogoMimeType { get; set; }
    }
}
