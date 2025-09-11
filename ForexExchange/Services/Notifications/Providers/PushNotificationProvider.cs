using ForexExchange.Services.Notifications;
using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebPush;

namespace ForexExchange.Services.Notifications.Providers
{
    /// <summary>
    /// Push notification provider using Web Push API
    /// ارائه‌دهنده اعلان فشاری با استفاده از Web Push API
    /// </summary>
    public class PushNotificationProvider : INotificationProvider
    {
        private readonly ForexDbContext _context;
        private readonly IVapidService _vapidService;
        private readonly ILogger<PushNotificationProvider> _logger;
        private readonly IConfiguration _configuration;
        private readonly WebPushClient _webPushClient;

        public string ProviderName => "Push";

        public bool IsEnabled => _configuration.GetValue<bool>("Notifications:Push:Enabled", true);

        public PushNotificationProvider(
            ForexDbContext context,
            IVapidService vapidService,
            ILogger<PushNotificationProvider> logger,
            IConfiguration configuration)
        {
            _context = context;
            _vapidService = vapidService;
            _logger = logger;
            _configuration = configuration;
            _webPushClient = new WebPushClient();
        }

        /// <summary>
        /// Initialize VAPID details for WebPush client
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
                _logger.LogError(ex, "Error initializing VAPID details for push notifications");
                throw;
            }
        }

        public async Task SendOrderNotificationAsync(NotificationContext context)
        {
            try
            {
                var payload = CreatePushPayload(context, "order");
                await SendPushNotificationAsync(context, payload, "Order Notification");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push order notification: {Title}", context.Title);
                throw;
            }
        }

        public async Task SendAccountingDocumentNotificationAsync(NotificationContext context)
        {
            try
            {
                var payload = CreatePushPayload(context, "document");
                await SendPushNotificationAsync(context, payload, "Document Notification");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push document notification: {Title}", context.Title);
                throw;
            }
        }

        public async Task SendCustomerNotificationAsync(NotificationContext context)
        {
            try
            {
                var payload = CreatePushPayload(context, "customer");
                await SendPushNotificationAsync(context, payload, "Customer Notification");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push customer notification: {Title}", context.Title);
                throw;
            }
        }

        public async Task SendSystemNotificationAsync(NotificationContext context)
        {
            try
            {
                var payload = CreatePushPayload(context, "system");
                await SendPushNotificationAsync(context, payload, "System Notification");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push system notification: {Title}", context.Title);
                throw;
            }
        }

        public async Task SendCustomNotificationAsync(NotificationContext context)
        {
            try
            {
                var payload = CreatePushPayload(context, "custom");
                await SendPushNotificationAsync(context, payload, "Custom Notification");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push custom notification: {Title}", context.Title);
                throw;
            }
        }

        private string CreatePushPayload(NotificationContext context, string notificationType)
        {
            var icon = GetIconForEventType(context.EventType);
            var badge = "/badge-72x72.png";

            return JsonSerializer.Serialize(new
            {
                title = context.Title,
                body = context.Message,
                icon = icon,
                badge = badge,
                tag = $"{notificationType}-{context.EventType}",
                data = new
                {
                    type = notificationType,
                    eventType = context.EventType.ToString(),
                    priority = context.Priority.ToString(),
                    timestamp = DateTime.UtcNow,
                    url = context.NavigationUrl ?? "/admin",
                    contextData = context.Data,
                    relatedEntity = context.RelatedEntity
                }
            });
        }

        private string GetIconForEventType(NotificationEventType eventType)
        {
            return eventType switch
            {
                NotificationEventType.OrderCreated => "/icons/order-created.png",
                NotificationEventType.OrderUpdated => "/icons/order-updated.png",
                NotificationEventType.OrderCompleted => "/icons/order-completed.png",
                NotificationEventType.OrderCancelled => "/icons/order-cancelled.png",
                NotificationEventType.AccountingDocumentCreated => "/icons/document-created.png",
                NotificationEventType.AccountingDocumentVerified => "/icons/document-verified.png",
                NotificationEventType.AccountingDocumentRejected => "/icons/document-rejected.png",
                NotificationEventType.CustomerRegistered => "/icons/customer-registered.png",
                NotificationEventType.CustomerBalanceChanged => "/icons/balance-changed.png",
                NotificationEventType.SystemError => "/icons/system-error.png",
                _ => "/icon-192x192.png"
            };
        }

        private async Task SendPushNotificationAsync(NotificationContext context, string payload, string logDescription)
        {
            try
            {
                // Initialize VAPID
                await InitializeVapidAsync();

                // Get target user IDs
                var targetUserIds = new List<string>();
                if (!string.IsNullOrEmpty(context.UserId))
                {
                    targetUserIds.Add(context.UserId);
                }
                targetUserIds.AddRange(context.TargetUserIds);

                if (context.SendToAllAdmins)
                {
                    // Get all admin user IDs
                    var adminUserIds = await GetAdminUserIdsAsync();
                    targetUserIds.AddRange(adminUserIds);
                }

                if (!targetUserIds.Any())
                {
                    _logger.LogWarning("No target users found for push notification: {Description}", logDescription);
                    return;
                }

                // Remove duplicates
                targetUserIds = targetUserIds.Distinct().ToList();

                var totalSuccessCount = 0;
                var totalErrorCount = 0;

                foreach (var userId in targetUserIds)
                {
                    var (successCount, errorCount) = await SendToUserAsync(userId, payload, logDescription);
                    totalSuccessCount += successCount;
                    totalErrorCount += errorCount;
                }

                _logger.LogInformation("{Description} push notification sent: {SuccessCount} successful, {ErrorCount} failed to {UserCount} users", 
                    logDescription, totalSuccessCount, totalErrorCount, targetUserIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification: {Description}", logDescription);
                throw;
            }
        }

        private async Task<(int successCount, int errorCount)> SendToUserAsync(string userId, string payload, string logDescription)
        {
            try
            {
                // Get user's active subscriptions
                var subscriptions = await _context.PushSubscriptions
                    .Where(ps => ps.UserId == userId && ps.IsActive)
                    .ToListAsync();

                if (!subscriptions.Any())
                {
                    _logger.LogDebug("No active push subscriptions found for user {UserId}", userId);
                    return (0, 0);
                }

                var successCount = 0;
                var errorCount = 0;

                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        var webPushSubscription = new WebPush.PushSubscription(
                            subscription.Endpoint,
                            subscription.P256dhKey,
                            subscription.AuthKey);

                        await _webPushClient.SendNotificationAsync(webPushSubscription, payload);
                        successCount++;

                        // Update subscription stats
                        subscription.SuccessfulNotifications++;
                        subscription.LastNotificationSent = DateTime.UtcNow;

                        // Log successful notification
                        var successLog = new PushNotificationLog
                        {
                            PushSubscriptionId = subscription.Id,
                            Title = logDescription,
                            Message = "Push notification sent successfully",
                            Type = "business_event",
                            Data = payload,
                            WasSuccessful = true,
                            ErrorMessage = string.Empty,
                            HttpStatusCode = 200,
                            SentAt = DateTime.UtcNow
                        };
                        _context.PushNotificationLogs.Add(successLog);
                    }
                    catch (WebPushException ex)
                    {
                        _logger.LogWarning(ex, "Failed to send push notification to endpoint {Endpoint} for user {UserId}", subscription.Endpoint, userId);
                        errorCount++;

                        // Update subscription stats
                        subscription.FailedNotifications++;

                        // Deactivate subscription if permanently failed
                        if (ex.StatusCode == System.Net.HttpStatusCode.Gone)
                        {
                            subscription.IsActive = false;
                            _logger.LogInformation("Deactivated expired push subscription for user {UserId}", userId);
                        }

                        // Log failed notification
                        var errorLog = new PushNotificationLog
                        {
                            PushSubscriptionId = subscription.Id,
                            Title = logDescription,
                            Message = "Push notification failed",
                            Type = "business_event",
                            Data = payload,
                            WasSuccessful = false,
                            ErrorMessage = ex.Message,
                            HttpStatusCode = (int?)ex.StatusCode,
                            SentAt = DateTime.UtcNow
                        };
                        _context.PushNotificationLogs.Add(errorLog);
                    }

                    subscription.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return (successCount, errorCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notifications to user {UserId}", userId);
                return (0, 1);
            }
        }

        private async Task<List<string>> GetAdminUserIdsAsync()
        {
            try
            {
                // Get all users in Admin, Manager, or Staff roles
                var adminUsers = await _context.Users
                    .Join(_context.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { User = u, UserRole = ur })
                    .Join(_context.Roles, ur => ur.UserRole.RoleId, r => r.Id, (ur, r) => new { ur.User, Role = r })
                    .Where(x => x.Role.Name == "Admin" || x.Role.Name == "Manager" || x.Role.Name == "Staff")
                    .Select(x => x.User.Id)
                    .Distinct()
                    .ToListAsync();

                return adminUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin user IDs for push notifications");
                return new List<string>();
            }
        }
    }
}
