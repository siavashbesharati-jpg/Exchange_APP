using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    /// <summary>
    /// VAPID configuration stored in database
    /// تنظیمات VAPID ذخیره شده در پایگاه داده
    /// </summary>
    [Table("VapidConfigurations")]
    public class VapidConfiguration
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Application identifier (usually just "main")
        /// شناسه اپلیکیشن (معمولاً فقط "main")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ApplicationId { get; set; } = "main";

        /// <summary>
        /// VAPID subject (contact email or website)
        /// موضوع VAPID (ایمیل تماس یا وب‌سایت)
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// VAPID public key (safe to expose)
        /// کلید عمومی VAPID (امن برای نمایش)
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string PublicKey { get; set; } = string.Empty;

        /// <summary>
        /// VAPID private key (KEEP SECRET!)
        /// کلید خصوصی VAPID (محرمانه نگه دارید!)
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string PrivateKey { get; set; } = string.Empty;

        /// <summary>
        /// When this configuration was created
        /// زمان ایجاد این تنظیمات
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this configuration was last updated
        /// زمان آخرین بروزرسانی این تنظیمات
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this configuration is currently active
        /// آیا این تنظیمات در حال حاضر فعال است
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Number of notifications sent using this key pair
        /// تعداد اعلان‌های ارسال شده با استفاده از این جفت کلید
        /// </summary>
        public long NotificationsSent { get; set; } = 0;

        /// <summary>
        /// When this key pair expires (for key rotation)
        /// زمان انقضای این جفت کلید (برای چرخش کلید)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Notes about this configuration
        /// یادداشت‌هایی درباره این تنظیمات
        /// </summary>
        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;
    }
}
