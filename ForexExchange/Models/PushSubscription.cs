using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    /// <summary>
    /// Push notification subscription entity
    /// موجودیت اشتراک اعلان‌های فشاری
    /// </summary>
    [Table("PushSubscriptions")]
    public class PushSubscription
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// User ID who owns this subscription
        /// شناسه کاربر مالک این اشتراک
        /// </summary>
        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Push service endpoint URL
        /// آدرس endpoint سرویس فشاری
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// P256DH key for encryption
        /// کلید P256DH برای رمزنگاری
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string P256dhKey { get; set; } = string.Empty;

        /// <summary>
        /// Auth key for encryption
        /// کلید احراز هویت برای رمزنگاری
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string AuthKey { get; set; } = string.Empty;

        /// <summary>
        /// User agent of the browser/device
        /// User agent مرورگر/دستگاه
        /// </summary>
        [MaxLength(500)]
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>
        /// Whether this subscription is active
        /// آیا این اشتراک فعال است
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// When this subscription was created
        /// زمان ایجاد این اشتراک
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this subscription was last updated
        /// زمان آخرین بروزرسانی این اشتراک
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last time a notification was sent to this subscription
        /// آخرین بار که اعلانی به این اشتراک ارسال شد
        /// </summary>
        public DateTime? LastNotificationSent { get; set; }

        /// <summary>
        /// Number of successful notifications sent
        /// تعداد اعلان‌های موفق ارسال شده
        /// </summary>
        public int SuccessfulNotifications { get; set; } = 0;

        /// <summary>
        /// Number of failed notification attempts
        /// تعداد تلاش‌های ناموفق ارسال اعلان
        /// </summary>
        public int FailedNotifications { get; set; } = 0;

        /// <summary>
        /// Device/browser type (Mobile, Desktop, etc.)
        /// نوع دستگاه/مرورگر (موبایل، دسکتاپ، و غیره)
        /// </summary>
        [MaxLength(50)]
        public string DeviceType { get; set; } = string.Empty;

        /// <summary>
        /// Navigation property to ApplicationUser
        /// ویژگی ناوبری به ApplicationUser
        /// </summary>
        public virtual ApplicationUser? User { get; set; }
    }

    /// <summary>
    /// Push notification log entity for tracking sent notifications
    /// موجودیت لاگ اعلان‌های فشاری برای پیگیری اعلان‌های ارسال شده
    /// </summary>
    [Table("PushNotificationLogs")]
    public class PushNotificationLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID of the push subscription
        /// شناسه اشتراک فشاری
        /// </summary>
        public int PushSubscriptionId { get; set; }

        /// <summary>
        /// Navigation property to PushSubscription
        /// ویژگی ناوبری به PushSubscription
        /// </summary>
        public virtual PushSubscription? PushSubscription { get; set; }

        /// <summary>
        /// Notification title
        /// عنوان اعلان
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Notification message
        /// متن اعلان
        /// </summary>
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Notification type (success, error, warning, info)
        /// نوع اعلان (موفقیت، خطا، هشدار، اطلاع)
        /// </summary>
        [MaxLength(20)]
        public string Type { get; set; } = "info";

        /// <summary>
        /// Additional data sent with notification (JSON)
        /// داده‌های اضافی ارسال شده با اعلان (JSON)
        /// </summary>
        [Column(TypeName = "TEXT")]
        public string Data { get; set; } = string.Empty;

        /// <summary>
        /// Whether the notification was sent successfully
        /// آیا اعلان با موفقیت ارسال شد
        /// </summary>
        public bool WasSuccessful { get; set; } = false;

        /// <summary>
        /// Error message if sending failed
        /// پیام خطا در صورت ناموفق بودن ارسال
        /// </summary>
        [MaxLength(500)]
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// HTTP status code returned by push service
        /// کد وضعیت HTTP بازگردانده شده توسط سرویس فشاری
        /// </summary>
        public int? HttpStatusCode { get; set; }

        /// <summary>
        /// When this notification was sent
        /// زمان ارسال این اعلان
        /// </summary>
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Time taken to send the notification (in milliseconds)
        /// زمان صرف شده برای ارسال اعلان (به میلی‌ثانیه)
        /// </summary>
        public int? SendDurationMs { get; set; }
    }
}
