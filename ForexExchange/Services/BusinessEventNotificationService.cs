using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using System.Text.Json;
using WebPush;
using System.Security.Claims;

namespace ForexExchange.Services
{
    /// <summary>
    /// Service for sending push notifications for business events
    /// سرویس ارسال اعلان‌های فشاری برای رویدادهای تجاری
    /// </summary>
    public interface IBusinessEventNotificationService
    {
        Task SendOrderCreatedNotificationAsync(Order order, string userId);
        Task SendAccountingDocumentCreatedNotificationAsync(AccountingDocument document, string userId);
        Task SendOrderStatusChangedNotificationAsync(Order order, string oldStatus, string newStatus, string userId);
        Task SendAccountingDocumentVerifiedNotificationAsync(AccountingDocument document, string userId);
    }

    public class BusinessEventNotificationService : IBusinessEventNotificationService
    {
        private readonly ForexDbContext _context;
        private readonly IVapidService _vapidService;
        private readonly ILogger<BusinessEventNotificationService> _logger;
        private readonly WebPushClient _webPushClient;

        public BusinessEventNotificationService(
            ForexDbContext context,
            IVapidService vapidService,
            ILogger<BusinessEventNotificationService> logger)
        {
            _context = context;
            _vapidService = vapidService;
            _logger = logger;
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
                _logger.LogError(ex, "Error initializing VAPID details for business notifications");
                throw;
            }
        }

        /// <summary>
        /// Send push notification for new order creation
        /// ارسال اعلان برای ایجاد سفارش جدید
        /// </summary>
        public async Task SendOrderCreatedNotificationAsync(Order order, string userId)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(order.CustomerId);
                var fromCurrency = await _context.Currencies.FindAsync(order.FromCurrencyId);
                var toCurrency = await _context.Currencies.FindAsync(order.ToCurrencyId);

                var title = "🔔 سفارش جدید ثبت شد";
                var body = $"سفارش #{order.Id} برای {customer?.FullName ?? "نامعلوم"}: {order.Amount:N0} {fromCurrency?.Symbol} → {order.TotalAmount:N0} {toCurrency?.Symbol}";

                var payload = JsonSerializer.Serialize(new
                {
                    title = title,
                    body = body,
                    icon = "/icon-192x192.png",
                    badge = "/badge-72x72.png",
                    tag = $"order-{order.Id}",
                    data = new
                    {
                        type = "order_created",
                        orderId = order.Id,
                        customerId = order.CustomerId,
                        timestamp = DateTime.UtcNow,
                        url = $"/Orders/Details/{order.Id}"
                    }
                });

                await SendNotificationToUserAsync(userId, payload, $"Order #{order.Id} Created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending order created notification for order {OrderId}", order.Id);
            }
        }

        /// <summary>
        /// Send push notification for new accounting document creation
        /// ارسال اعلان برای ایجاد سند حسابداری جدید
        /// </summary>
        public async Task SendAccountingDocumentCreatedNotificationAsync(AccountingDocument document, string userId)
        {
            try
            {
                var payerCustomer = await _context.Customers.FindAsync(document.PayerCustomerId);
                var receiverCustomer = await _context.Customers.FindAsync(document.ReceiverCustomerId);
                var currency = await _context.Currencies.FindAsync(document.CurrencyCode);

                var title = "📄 سند حسابداری جدید";
                var body = $"{document.Title}: {document.Amount:N0} {currency?.Symbol ?? document.CurrencyCode}";
                
                if (payerCustomer != null)
                {
                    body += $" از {payerCustomer.FullName}";
                }
                if (receiverCustomer != null)
                {
                    body += $" به {receiverCustomer.FullName}";
                }

                var payload = JsonSerializer.Serialize(new
                {
                    title = title,
                    body = body,
                    icon = "/icon-192x192.png",
                    badge = "/badge-72x72.png",
                    tag = $"document-{document.Id}",
                    data = new
                    {
                        type = "document_created",
                        documentId = document.Id,
                        payerCustomerId = document.PayerCustomerId,
                        receiverCustomerId = document.ReceiverCustomerId,
                        timestamp = DateTime.UtcNow,
                        url = $"/AccountingDocuments/Details/{document.Id}"
                    }
                });

                await SendNotificationToUserAsync(userId, payload, $"Document #{document.Id} Created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document created notification for document {DocumentId}", document.Id);
            }
        }

        /// <summary>
        /// Send push notification for order status change
        /// ارسال اعلان برای تغییر وضعیت سفارش
        /// </summary>
        public async Task SendOrderStatusChangedNotificationAsync(Order order, string oldStatus, string newStatus, string userId)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(order.CustomerId);

                var title = "🔄 تغییر وضعیت سفارش";
                var body = $"سفارش #{order.Id} ({customer?.FullName}): {oldStatus} → {newStatus}";

                var payload = JsonSerializer.Serialize(new
                {
                    title = title,
                    body = body,
                    icon = "/icon-192x192.png",
                    badge = "/badge-72x72.png",
                    tag = $"order-status-{order.Id}",
                    data = new
                    {
                        type = "order_status_changed",
                        orderId = order.Id,
                        customerId = order.CustomerId,
                        oldStatus = oldStatus,
                        newStatus = newStatus,
                        timestamp = DateTime.UtcNow,
                        url = $"/Orders/Details/{order.Id}"
                    }
                });

                await SendNotificationToUserAsync(userId, payload, $"Order #{order.Id} Status Changed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending order status change notification for order {OrderId}", order.Id);
            }
        }

        /// <summary>
        /// Send push notification for accounting document verification
        /// ارسال اعلان برای تأیید سند حسابداری
        /// </summary>
        public async Task SendAccountingDocumentVerifiedNotificationAsync(AccountingDocument document, string userId)
        {
            try
            {
                var payerCustomer = await _context.Customers.FindAsync(document.PayerCustomerId);
                var currency = await _context.Currencies.FindAsync(document.CurrencyCode);

                var title = "✅ تأیید سند حسابداری";
                var body = $"{document.Title}: {document.Amount:N0} {currency?.Symbol ?? document.CurrencyCode} تأیید شد";

                var payload = JsonSerializer.Serialize(new
                {
                    title = title,
                    body = body,
                    icon = "/icon-192x192.png",
                    badge = "/badge-72x72.png",
                    tag = $"document-verified-{document.Id}",
                    data = new
                    {
                        type = "document_verified",
                        documentId = document.Id,
                        payerCustomerId = document.PayerCustomerId,
                        receiverCustomerId = document.ReceiverCustomerId,
                        timestamp = DateTime.UtcNow,
                        url = $"/AccountingDocuments/Details/{document.Id}"
                    }
                });

                await SendNotificationToUserAsync(userId, payload, $"Document #{document.Id} Verified");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending document verified notification for document {DocumentId}", document.Id);
            }
        }

        /// <summary>
        /// Send notification to a specific user
        /// ارسال اعلان به کاربر مشخص
        /// </summary>
        private async Task SendNotificationToUserAsync(string userId, string payload, string logDescription)
        {
            try
            {
                // Initialize VAPID
                await InitializeVapidAsync();

                // Get user's active subscriptions
                var subscriptions = await _context.PushSubscriptions
                    .Where(ps => ps.UserId == userId && ps.IsActive)
                    .ToListAsync();

                if (!subscriptions.Any())
                {
                    _logger.LogInformation("No active subscriptions found for user {UserId} for notification: {Description}", userId, logDescription);
                    return;
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

                        // Log to PushNotificationLogs
                        var log = new PushNotificationLog
                        {
                            PushSubscriptionId = subscription.Id,
                            Title = "Business Event Notification",
                            Message = logDescription,
                            Type = "business_event",
                            Data = payload,
                            WasSuccessful = true,
                            ErrorMessage = string.Empty,
                            HttpStatusCode = 200,
                            SentAt = DateTime.UtcNow
                        };
                        _context.PushNotificationLogs.Add(log);
                    }
                    catch (WebPushException ex)
                    {
                        _logger.LogWarning(ex, "Failed to send business notification to endpoint {Endpoint} for user {UserId}", subscription.Endpoint, userId);
                        errorCount++;

                        // Update subscription stats
                        subscription.FailedNotifications++;

                        // Deactivate subscription if permanently failed
                        if (ex.StatusCode == System.Net.HttpStatusCode.Gone)
                        {
                            subscription.IsActive = false;
                            _logger.LogInformation("Deactivated expired subscription for user {UserId}", userId);
                        }

                        // Log failed notification
                        var log = new PushNotificationLog
                        {
                            PushSubscriptionId = subscription.Id,
                            Title = "Business Event Notification",
                            Message = logDescription,
                            Type = "business_event",
                            Data = payload,
                            WasSuccessful = false,
                            ErrorMessage = ex.Message,
                            HttpStatusCode = (int?)ex.StatusCode,
                            SentAt = DateTime.UtcNow
                        };
                        _context.PushNotificationLogs.Add(log);
                    }

                    subscription.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("{Description} notification sent to user {UserId}: {SuccessCount} successful, {ErrorCount} failed", 
                    logDescription, userId, successCount, errorCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification to user {UserId}: {Description}", userId, logDescription);
            }
        }
    }
}
