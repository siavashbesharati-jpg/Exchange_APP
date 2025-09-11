using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace ForexExchange.Services
{
    /// <summary>
    /// Service for managing VAPID configurations
    /// سرویس مدیریت تنظیمات VAPID
    /// </summary>
    public interface IVapidService
    {
        Task<VapidConfiguration> GetOrCreateVapidConfigurationAsync();
        Task<VapidConfiguration?> GetActiveVapidConfigurationAsync();
        Task<VapidConfiguration> CreateNewVapidConfigurationAsync(string? customSubject = null);
        Task<bool> RotateVapidKeysAsync();
        Task<VapidDetails> GetVapidDetailsAsync();
        Task<VapidStatistics> GetVapidStatisticsAsync();
    }

    /// <summary>
    /// Implementation of VAPID management service
    /// پیاده‌سازی سرویس مدیریت VAPID
    /// </summary>
    public class VapidService : IVapidService
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<VapidService> _logger;
        private readonly IConfiguration _configuration;
        private VapidConfiguration? _cachedConfiguration;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public VapidService(
            ForexDbContext context,
            ILogger<VapidService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Get existing VAPID configuration or create new one
        /// دریافت تنظیمات VAPID موجود یا ایجاد جدید
        /// </summary>
        public async Task<VapidConfiguration> GetOrCreateVapidConfigurationAsync()
        {
            // Check cache first
            if (_cachedConfiguration != null)
            {
                return _cachedConfiguration;
            }

            await _semaphore.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (_cachedConfiguration != null)
                {
                    return _cachedConfiguration;
                }

                // Try to get existing active configuration
                var existingConfig = await GetActiveVapidConfigurationAsync();
                if (existingConfig != null)
                {
                    _cachedConfiguration = existingConfig;
                    _logger.LogInformation("Using existing VAPID configuration from database");
                    return existingConfig;
                }

                // Create new configuration
                _logger.LogInformation("No VAPID configuration found. Generating new keys...");
                var newConfig = await CreateNewVapidConfigurationAsync();
                _cachedConfiguration = newConfig;
                
                _logger.LogInformation("New VAPID configuration created and stored in database");
                return newConfig;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Get active VAPID configuration from database
        /// دریافت تنظیمات VAPID فعال از پایگاه داده
        /// </summary>
        public async Task<VapidConfiguration?> GetActiveVapidConfigurationAsync()
        {
            return await _context.VapidConfigurations
                .Where(v => v.IsActive && v.ApplicationId == "main")
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Create new VAPID configuration with generated keys
        /// ایجاد تنظیمات VAPID جدید با کلیدهای تولید شده
        /// </summary>
        public async Task<VapidConfiguration> CreateNewVapidConfigurationAsync(string? customSubject = null)
        {
            try
            {
                // Generate new VAPID key pair using WebPush library
                var vapidKeys = VapidHelper.GenerateVapidKeys();
                
                // Get subject from configuration or use custom
                var subject = customSubject ?? 
                             _configuration["VAPID:Subject"] ?? 
                             "mailto:admin@tabanexchange.com";

                // Ensure subject has proper format
                if (!subject.StartsWith("mailto:") && !subject.StartsWith("https://"))
                {
                    subject = $"mailto:{subject}";
                }

                // Deactivate any existing configurations
                var existingConfigs = await _context.VapidConfigurations
                    .Where(v => v.ApplicationId == "main")
                    .ToListAsync();

                foreach (var config in existingConfigs)
                {
                    config.IsActive = false;
                    config.UpdatedAt = DateTime.UtcNow;
                }

                // Create new configuration
                var newConfig = new VapidConfiguration
                {
                    ApplicationId = "main",
                    Subject = subject,
                    PublicKey = vapidKeys.PublicKey,
                    PrivateKey = vapidKeys.PrivateKey,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true,
                    ExpiresAt = DateTime.UtcNow.AddYears(1), // Keys valid for 1 year
                    Notes = $"Auto-generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
                };

                _context.VapidConfigurations.Add(newConfig);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New VAPID configuration created with ID {Id}", newConfig.Id);
                return newConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new VAPID configuration");
                throw;
            }
        }

        /// <summary>
        /// Rotate VAPID keys (create new configuration)
        /// چرخش کلیدهای VAPID (ایجاد تنظیمات جدید)
        /// </summary>
        public async Task<bool> RotateVapidKeysAsync()
        {
            try
            {
                _logger.LogInformation("Starting VAPID key rotation...");
                
                var newConfig = await CreateNewVapidConfigurationAsync();
                
                // Clear cache to force reload
                _cachedConfiguration = null;
                
                _logger.LogInformation("VAPID key rotation completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during VAPID key rotation");
                return false;
            }
        }

        /// <summary>
        /// Get VAPID details for WebPush client
        /// دریافت جزئیات VAPID برای کلاینت WebPush
        /// </summary>
        public async Task<VapidDetails> GetVapidDetailsAsync()
        {
            var config = await GetOrCreateVapidConfigurationAsync();
            
            return new VapidDetails
            {
                Subject = config.Subject,
                PublicKey = config.PublicKey,
                PrivateKey = config.PrivateKey
            };
        }

        /// <summary>
        /// Increment notification counter for active configuration
        /// افزایش شمارنده اعلان برای تنظیمات فعال
        /// </summary>
        public async Task IncrementNotificationCountAsync()
        {
            try
            {
                var config = await GetActiveVapidConfigurationAsync();
                if (config != null)
                {
                    config.NotificationsSent++;
                    config.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error incrementing notification count");
                // Don't throw - this is not critical
            }
        }

        /// <summary>
        /// Check if current VAPID configuration is expired
        /// بررسی اینکه آیا تنظیمات VAPID فعلی منقضی شده است
        /// </summary>
        public async Task<bool> IsVapidConfigurationExpiredAsync()
        {
            var config = await GetActiveVapidConfigurationAsync();
            if (config == null) return true;

            return config.ExpiresAt.HasValue && config.ExpiresAt.Value <= DateTime.UtcNow;
        }

        /// <summary>
        /// Get VAPID configuration statistics
        /// دریافت آمار تنظیمات VAPID
        /// </summary>
        public async Task<VapidStatistics> GetVapidStatisticsAsync()
        {
            var activeConfig = await GetActiveVapidConfigurationAsync();
            var totalConfigs = await _context.VapidConfigurations.CountAsync();
            var totalNotifications = await _context.VapidConfigurations
                .SumAsync(v => v.NotificationsSent);

            return new VapidStatistics
            {
                HasActiveConfiguration = activeConfig != null,
                ActiveConfigurationId = activeConfig?.Id,
                CreatedAt = activeConfig?.CreatedAt,
                ExpiresAt = activeConfig?.ExpiresAt,
                NotificationsSent = activeConfig?.NotificationsSent ?? 0,
                TotalConfigurations = totalConfigs,
                TotalNotificationsSent = totalNotifications,
                IsExpired = activeConfig?.ExpiresAt.HasValue == true && 
                           activeConfig.ExpiresAt.Value <= DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// VAPID details for WebPush client
    /// جزئیات VAPID برای کلاینت WebPush
    /// </summary>
    public class VapidDetails
    {
        public string Subject { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// VAPID configuration statistics
    /// آمار تنظیمات VAPID
    /// </summary>
    public class VapidStatistics
    {
        public bool HasActiveConfiguration { get; set; }
        public int? ActiveConfigurationId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public long NotificationsSent { get; set; }
        public int TotalConfigurations { get; set; }
        public long TotalNotificationsSent { get; set; }
        public bool IsExpired { get; set; }
    }
}
