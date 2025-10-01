using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForexExchange.Models
{
    public enum ShareableLinkType
    {
        [Display(Name = "صورت حساب مشتری")]
        CustomerReport,
       
    }

    /// <summary>
    /// Shareable Link for customer statements accessible without login
    /// </summary>
    public class ShareableLink
    {
        public int Id { get; set; }

        [Required]
        [StringLength(128)]
        [Display(Name = "Unique Token - توکن یکتا")]
        public string Token { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Customer - مشتری")]
        public int CustomerId { get; set; }

        [Required]
        [Display(Name = "Link Type - نوع لینک")]
        public ShareableLinkType LinkType { get; set; }

        [Required]
        [Display(Name = "Created At - تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; }

        [Required]
        [Display(Name = "Expires At - تاریخ انقضا")]
        public DateTime ExpiresAt { get; set; }

        [Display(Name = "Is Active - فعال")]
        public bool IsActive { get; set; } = true;

        [StringLength(100)]
        [Display(Name = "Created By - ایجاد شده توسط")]
        public string? CreatedBy { get; set; }

        [Display(Name = "Access Count - تعداد دسترسی")]
        public int AccessCount { get; set; } = 0;

        [Display(Name = "Last Accessed - آخرین دسترسی")]
        public DateTime? LastAccessedAt { get; set; }

        [StringLength(200)]
        [Display(Name = "Description - توضیحات")]
        public string? Description { get; set; }

        // Navigation Properties
        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; } = null!;

        /// <summary>
        /// Check if the link is valid (active and not expired)
        /// </summary>
        public bool IsValid => IsActive && DateTime.Now <= ExpiresAt;

        /// <summary>
        /// Generate a new secure random token
        /// </summary>
        public static string GenerateToken()
        {
            // Generate a cryptographically secure random token
            var bytes = new byte[64]; // 512 bits
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            
            // Convert to base64 and make URL-safe
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "")
                .Substring(0, 32); // Take first 32 characters for manageable URL length
        }

        /// <summary>
        /// Get the full URL for this shareable link
        /// </summary>
        public string GetShareableUrl(string baseUrl)
        {
            var linkTypeUrl = LinkType switch
            {
                ShareableLinkType.CustomerReport => "CustomerReports",
                _ => "unknown"
            };
            
            return $"{baseUrl.TrimEnd('/')}/Share/{linkTypeUrl}/{Token}";
        }
    }
}
