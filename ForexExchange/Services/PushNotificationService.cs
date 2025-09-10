using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;
using WebPush;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ForexExchange.Services
{
    /// <summary>
    /// Service for managing and sending push notifications
    /// سرویس مدیریت و ارسال اعلان‌های فشاری
    /// </summary>
    public interface IPushNotificationService
    {
        Task SendToUserAsync(string userId, string title, string message, string type = "info", object? data = null);
        Task SendToAllAdminsAsync(string title, string message, string type = "info", object? data = null);
        Task SendToRoleAsync(string role, string title, string message, string type = "info", object? data = null);
        Task CleanupInvalidSubscriptionsAsync();
        Task<int> GetActiveSubscriptionCountAsync();
        Task<bool> IsUserSubscribedAsync(string userId);
    }

    /// <summary>
    /// Implementation of push notification service
    /// پیاده‌سازی سرویس اعلان‌های فشاری
    /// </summary>
    public class PushNotificationService : IPushNotificationService
    {
        private readonly ForexDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly IVapidService _vapidService;
        private readonly WebPushClient _webPushClient;

        public PushNotificationService(
            ForexDbContext context,
            IConfiguration configuration,
            ILogger<PushNotificationService> logger,
            IVapidService vapidService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _vapidService = vapidService;

            // Initialize WebPush client
            _webPushClient = new WebPushClient();
        }

        /// <summary>
        /// Initialize VAPID details for WebPush client
        /// راه‌اندازی جزئیات VAPID برای کلاینت WebPush
        /// </summary>
        private async Task InitializeVapidAsync()
        {
            try
            {
                var vapidDetails = await _vapidService.GetVapidDetailsAsync();
                _webPushClient.SetVapidDetails(vapidDetails.Subject, vapidDetails.PublicKey, vapidDetails.PrivateKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing VAPID details");
                throw;
            }
        }

        /// <summary>
        /// Send push notification to specific user
        /// ارسال اعلان فشاری به کاربر خاص
        /// </summary>
        public async Task SendToUserAsync(string userId, string title, string message, string type = "info", object? data = null)
        {
            try
            {
                var subscriptions = await _context.PushSubscriptions
                    .Where(ps => ps.UserId == userId && ps.IsActive)
                    .ToListAsync();

                if (!subscriptions.Any())
                {
                    _logger.LogInformation("No active push subscriptions found for user {UserId}", userId);
                    return;
                }

                var payload = CreateNotificationPayload(title, message, type, data);
                await SendToSubscriptionsAsync(subscriptions, payload);

                _logger.LogInformation("Push notification sent to {Count} subscriptions for user {UserId}", 
                    subscriptions.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification to user {UserId}", userId);
            }
        }

        /// <summary>
        /// Send push notification to all admin users
        /// ارسال اعلان فشاری به تمام کاربران ادمین
        /// </summary>
        public async Task SendToAllAdminsAsync(string title, string message, string type = "info", object? data = null)
        {
            try
            {
                var adminRoles = new[] { "Admin", "Manager", "Staff" };
                var subscriptions = await _context.PushSubscriptions
                    .Include(ps => ps.User)
                    .Where(ps => ps.IsActive && ps.User != null && 
                                _context.UserRoles.Any(ur => ur.UserId == ps.User.Id &&
                                _context.Roles.Any(r => r.Id == ur.RoleId && adminRoles.Contains(r.Name))))
                    .ToListAsync();

                if (!subscriptions.Any())
                {
                    _logger.LogInformation("No active push subscriptions found for admin users");
                    return;
                }

                var payload = CreateNotificationPayload(title, message, type, data);
                await SendToSubscriptionsAsync(subscriptions, payload);

                _logger.LogInformation("Push notification sent to {Count} admin subscriptions", subscriptions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification to admin users");
            }
        }

        /// <summary>
        /// Send push notification to users with specific role
        /// ارسال اعلان فشاری به کاربران با نقش خاص
        /// </summary>
        public async Task SendToRoleAsync(string role, string title, string message, string type = "info", object? data = null)
        {
            try
            {
                var subscriptions = await _context.PushSubscriptions
                    .Include(ps => ps.User)
                    .Where(ps => ps.IsActive && ps.User != null && 
                                _context.UserRoles.Any(ur => ur.UserId == ps.User.Id &&
                                _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == role)))
                    .ToListAsync();

                if (!subscriptions.Any())
                {
                    _logger.LogInformation("No active push subscriptions found for role {Role}", role);
                    return;
                }

                var payload = CreateNotificationPayload(title, message, type, data);
                await SendToSubscriptionsAsync(subscriptions, payload);

                _logger.LogInformation("Push notification sent to {Count} subscriptions for role {Role}", 
                    subscriptions.Count, role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification to role {Role}", role);
            }
        }

        /// <summary>
        /// Clean up invalid/expired push subscriptions
        /// پاک‌سازی اشتراک‌های نامعتبر/منقضی
        /// </summary>
        public async Task CleanupInvalidSubscriptionsAsync()
        {
            try
            {
                // Deactivate subscriptions older than 90 days without successful notifications
                var cutoffDate = DateTime.UtcNow.AddDays(-90);
                var staleSubscriptions = await _context.PushSubscriptions
                    .Where(ps => ps.IsActive && 
                                ps.CreatedAt < cutoffDate && 
                                (ps.LastNotificationSent == null || ps.SuccessfulNotifications == 0))
                    .ToListAsync();

                foreach (var subscription in staleSubscriptions)
                {
                    subscription.IsActive = false;
                    subscription.UpdatedAt = DateTime.UtcNow;
                }

                // Remove very old inactive subscriptions (6 months)
                var removeDate = DateTime.UtcNow.AddMonths(-6);
                var oldSubscriptions = await _context.PushSubscriptions
                    .Where(ps => !ps.IsActive && ps.UpdatedAt < removeDate)
                    .ToListAsync();

                if (oldSubscriptions.Any())
                {
                    _context.PushSubscriptions.RemoveRange(oldSubscriptions);
                }

                var changes = staleSubscriptions.Count + oldSubscriptions.Count;
                if (changes > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Cleaned up {Count} push subscriptions", changes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up push subscriptions");
            }
        }

        /// <summary>
        /// Get count of active push subscriptions
        /// دریافت تعداد اشتراک‌های فعال فشاری
        /// </summary>
        public async Task<int> GetActiveSubscriptionCountAsync()
        {
            try
            {
                return await _context.PushSubscriptions
                    .Where(ps => ps.IsActive)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active subscription count");
                return 0;
            }
        }

        /// <summary>
        /// Check if user has active push subscriptions
        /// بررسی اینکه آیا کاربر اشتراک فعال دارد
        /// </summary>
        public async Task<bool> IsUserSubscribedAsync(string userId)
        {
            try
            {
                return await _context.PushSubscriptions
                    .AnyAsync(ps => ps.UserId == userId && ps.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user subscription status for {UserId}", userId);
                return false;
            }
        }

        /// <summary>
        /// Create notification payload
        /// ایجاد محتوای اعلان
        /// </summary>
        private string CreateNotificationPayload(string title, string message, string type, object? data)
        {
            var payload = new
            {
                title = title,
                message = message,
                type = type,
                timestamp = DateTime.UtcNow,
                data = data ?? new { },
                icon = "/favicon/apple-touch-icon.png",
                badge = "/favicon/favicon-32x32.png",
                tag = "taban-notification",
                requireInteraction = type == "error" || type == "warning"
            };

            return JsonSerializer.Serialize(payload);
        }

        /// <summary>
        /// Send notification to list of subscriptions
        /// ارسال اعلان به لیست اشتراک‌ها
        /// </summary>
        private async Task SendToSubscriptionsAsync(List<Models.PushSubscription> subscriptions, string payload)
        {
            var tasks = subscriptions.Select(subscription => SendToSubscriptionAsync(subscription, payload));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Send notification to single subscription
        /// ارسال اعلان به یک اشتراک
        /// </summary>
        private async Task SendToSubscriptionAsync(Models.PushSubscription subscription, string payload)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var log = new PushNotificationLog
            {
                PushSubscriptionId = subscription.Id,
                Title = GetTitleFromPayload(payload),
                Message = GetMessageFromPayload(payload),
                Type = GetTypeFromPayload(payload),
                Data = payload,
                SentAt = DateTime.UtcNow
            };

            try
            {
                // Ensure VAPID is initialized
                await InitializeVapidAsync();

                var webPushSubscription = new WebPush.PushSubscription(
                    subscription.Endpoint,
                    subscription.P256dhKey,
                    subscription.AuthKey);

                await _webPushClient.SendNotificationAsync(webPushSubscription, payload);

                // Update subscription stats
                subscription.SuccessfulNotifications++;
                subscription.LastNotificationSent = DateTime.UtcNow;
                subscription.UpdatedAt = DateTime.UtcNow;

                // Update log
                log.WasSuccessful = true;
                log.HttpStatusCode = 200;
            }
            catch (WebPushException ex)
            {
                _logger.LogWarning(ex, "Failed to send push notification to endpoint {Endpoint}", subscription.Endpoint);

                // Update subscription stats
                subscription.FailedNotifications++;
                subscription.UpdatedAt = DateTime.UtcNow;

                // Deactivate subscription if it's invalid
                if (ex.StatusCode == System.Net.HttpStatusCode.Gone || 
                    ex.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                    ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    subscription.IsActive = false;
                    _logger.LogInformation("Deactivated invalid push subscription {Id}", subscription.Id);
                }

                // Update log
                log.WasSuccessful = false;
                log.ErrorMessage = ex.Message;
                log.HttpStatusCode = (int?)ex.StatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending push notification to subscription {Id}", subscription.Id);

                // Update subscription stats
                subscription.FailedNotifications++;
                subscription.UpdatedAt = DateTime.UtcNow;

                // Update log
                log.WasSuccessful = false;
                log.ErrorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                log.SendDurationMs = (int)stopwatch.ElapsedMilliseconds;

                // Save log and subscription updates
                _context.PushNotificationLogs.Add(log);
                _context.PushSubscriptions.Update(subscription);
                
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving push notification log");
                }
            }
        }

        /// <summary>
        /// Extract title from payload
        /// استخراج عنوان از محتوا
        /// </summary>
        private string GetTitleFromPayload(string payload)
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(payload);
                return json.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Extract message from payload
        /// استخراج پیام از محتوا
        /// </summary>
        private string GetMessageFromPayload(string payload)
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(payload);
                return json.TryGetProperty("message", out var message) ? message.GetString() ?? "" : "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Extract type from payload
        /// استخراج نوع از محتوا
        /// </summary>
        private string GetTypeFromPayload(string payload)
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(payload);
                return json.TryGetProperty("type", out var type) ? type.GetString() ?? "info" : "info";
            }
            catch
            {
                return "info";
            }
        }
    }
}
