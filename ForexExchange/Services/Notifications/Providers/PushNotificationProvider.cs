using ForexExchange.Services.Notifications;
using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PushNotificationProvider> _logger;
        private readonly IConfiguration _configuration;

        public string ProviderName => "Push";

        public bool IsEnabled => true;

        public PushNotificationProvider(
            IServiceScopeFactory scopeFactory,
            ILogger<PushNotificationProvider> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Initialize VAPID details for WebPush client
        /// </summary>
        private static async Task InitializeVapidAsync(WebPushClient webPushClient, IVapidService vapidService, ILogger logger)
        {
            try
            {
                var vapidDetails = await vapidService.GetVapidDetailsAsync();
                webPushClient.SetVapidDetails(vapidDetails.Subject, vapidDetails.PublicKey, vapidDetails.PrivateKey);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error initializing VAPID details for push notifications");
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

        public async Task SendManualAdjustmentNotificationAsync(NotificationContext context)
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

            _logger.LogInformation("Creating push payload for {Type}: Title={Title}, URL={Url}", notificationType, context.Title, context.NavigationUrl);

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
                NotificationEventType.OrderDeleted => "/icons/order-cancelled.png",
                NotificationEventType.AccountingDocumentCreated => "/icons/document-created.png",
                NotificationEventType.AccountingDocumentVerified => "/icons/document-verified.png",
                NotificationEventType.CustomerRegistered => "/icons/customer-registered.png",
                NotificationEventType.ManualAdjustment => "/icons/balance-changed.png",
                NotificationEventType.SystemError => "/icons/system-error.png",
                _ => "/icon-192x192.png"
            };
        }

        private Task SendPushNotificationAsync(NotificationContext context, string payload, string logDescription)
        {
            var userId = context.UserId;
            var explicitTargets = context.TargetUserIds?.ToList() ?? new List<string>();
            var excludeUserIds = context.ExcludeUserIds?.ToList() ?? new List<string>();
            var sendToAllAdmins = context.SendToAllAdmins;
            var eventType = context.EventType;

            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<PushNotificationProvider>>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ForexDbContext>();
                    var vapidService = scope.ServiceProvider.GetRequiredService<IVapidService>();
                    var webPushClient = new WebPushClient();

                    await InitializeVapidAsync(webPushClient, vapidService, scopedLogger);

                    var targetUserIds = await ResolveTargetUserIdsAsync(dbContext, scopedLogger, userId, explicitTargets, excludeUserIds, sendToAllAdmins);

                    if (!targetUserIds.Any())
                    {
                        scopedLogger.LogWarning("No target users found for push notification: {Description}", logDescription);
                        return;
                    }

                    var totalSuccessCount = 0;
                    var totalErrorCount = 0;

                    foreach (var targetUserId in targetUserIds)
                    {
                        var (successCount, errorCount) = await SendToUserAsync(dbContext, webPushClient, scopedLogger, targetUserId, payload, logDescription);
                        totalSuccessCount += successCount;
                        totalErrorCount += errorCount;
                    }

                    scopedLogger.LogInformation("{Description} push notification sent: {SuccessCount} successful, {ErrorCount} failed to {UserCount} users",
                        logDescription, totalSuccessCount, totalErrorCount, targetUserIds.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending push notification in background: {Description}", logDescription);
                }
            });

            return Task.CompletedTask;
        }

        private static async Task<List<string>> ResolveTargetUserIdsAsync(
            ForexDbContext dbContext,
            ILogger logger,
            string? userId,
            List<string> explicitTargets,
            List<string> excludeUserIds,
            bool sendToAllAdmins)
        {
            var targetUserIds = new List<string>();

            if (!string.IsNullOrEmpty(userId))
            {
                targetUserIds.Add(userId);
            }

            if (explicitTargets.Any())
            {
                targetUserIds.AddRange(explicitTargets);
            }

            if (sendToAllAdmins)
            {
                var adminUserIds = await GetAdminUserIdsAsync(dbContext, logger);
                targetUserIds.AddRange(adminUserIds);
            }

            if (excludeUserIds.Any())
            {
                targetUserIds = targetUserIds.Where(id => !excludeUserIds.Contains(id)).ToList();
            }

            return targetUserIds.Distinct().ToList();
        }

        private static async Task<(int successCount, int errorCount)> SendToUserAsync(
            ForexDbContext dbContext,
            WebPushClient webPushClient,
            ILogger logger,
            string userId,
            string payload,
            string logDescription)
        {
            try
            {
                var subscriptions = await dbContext.PushSubscriptions
                    .Where(ps => ps.UserId == userId && ps.IsActive)
                    .ToListAsync();

                if (!subscriptions.Any())
                {
                    logger.LogDebug("No active push subscriptions found for user {UserId}", userId);
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

                        await webPushClient.SendNotificationAsync(webPushSubscription, payload);
                        successCount++;

                        subscription.SuccessfulNotifications++;
                        subscription.LastNotificationSent = DateTime.UtcNow;

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
                        dbContext.PushNotificationLogs.Add(successLog);
                    }
                    catch (WebPushException ex)
                    {
                        logger.LogWarning(ex, "Failed to send push notification to endpoint {Endpoint} for user {UserId}", subscription.Endpoint, userId);
                        errorCount++;

                        subscription.FailedNotifications++;

                        if (ex.StatusCode == System.Net.HttpStatusCode.Gone)
                        {
                            subscription.IsActive = false;
                            logger.LogInformation("Deactivated expired push subscription for user {UserId}", userId);
                        }

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
                        dbContext.PushNotificationLogs.Add(errorLog);
                    }
                    catch (TaskCanceledException ex)
                    {
                        logger.LogWarning(ex, "Push notification timed out for endpoint {Endpoint} (user {UserId})", subscription.Endpoint, userId);
                        errorCount++;

                        subscription.FailedNotifications++;

                        var errorLog = new PushNotificationLog
                        {
                            PushSubscriptionId = subscription.Id,
                            Title = logDescription,
                            Message = "Push notification timed out",
                            Type = "business_event",
                            Data = payload,
                            WasSuccessful = false,
                            ErrorMessage = ex.Message,
                            HttpStatusCode = null,
                            SentAt = DateTime.UtcNow
                        };
                        dbContext.PushNotificationLogs.Add(errorLog);
                    }

                    subscription.UpdatedAt = DateTime.UtcNow;
                }

                await dbContext.SaveChangesAsync();
                return (successCount, errorCount);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending push notifications to user {UserId}", userId);
                return (0, 1);
            }
        }

        private static async Task<List<string>> GetAdminUserIdsAsync(ForexDbContext dbContext, ILogger logger)
        {
            try
            {
                var adminUsers = await dbContext.Users
                    .Join(dbContext.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { User = u, UserRole = ur })
                    .Join(dbContext.Roles, ur => ur.UserRole.RoleId, r => r.Id, (ur, r) => new { ur.User, Role = r })
                    .Where(x => x.Role.Name == "Admin" || x.Role.Name == "Manager" || x.Role.Name == "Staff")
                    .Select(x => x.User.Id)
                    .Distinct()
                    .ToListAsync();

                return adminUsers;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting admin user IDs for push notifications");
                return new List<string>();
            }
        }
    }
}
