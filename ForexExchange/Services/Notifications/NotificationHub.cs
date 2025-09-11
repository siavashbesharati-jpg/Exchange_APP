using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services.Notifications
{
    /// <summary>
    /// Central notification hub interface
    /// رابط مرکز اعلان‌های مرکزی
    /// </summary>
    public interface INotificationHub
    {
        /// <summary>
        /// Send notification for order events
        /// ارسال اعلان برای رویدادهای سفارش
        /// </summary>
        Task SendOrderNotificationAsync(Order order, NotificationEventType eventType, string? userId = null, string? oldStatus = null, string? newStatus = null);

        /// <summary>
        /// Send notification for accounting document events
        /// ارسال اعلان برای رویدادهای سند حسابداری
        /// </summary>
        Task SendAccountingDocumentNotificationAsync(AccountingDocument document, NotificationEventType eventType, string? userId = null);

        /// <summary>
        /// Send notification for customer events
        /// ارسال اعلان برای رویدادهای مشتری
        /// </summary>
        Task SendCustomerNotificationAsync(Customer customer, NotificationEventType eventType, string? userId = null);

        /// <summary>
        /// Send custom notification
        /// ارسال اعلان سفارشی
        /// </summary>
        Task SendCustomNotificationAsync(string title, string message, NotificationEventType eventType = NotificationEventType.Custom, string? userId = null, string? navigationUrl = null, NotificationPriority priority = NotificationPriority.Normal);

        /// <summary>
        /// Register a notification provider
        /// ثبت ارائه‌دهنده اعلان
        /// </summary>
        void RegisterProvider(INotificationProvider provider);

        /// <summary>
        /// Get all registered providers
        /// دریافت همه ارائه‌دهندگان ثبت شده
        /// </summary>
        IEnumerable<INotificationProvider> GetProviders();

        /// <summary>
        /// Enable or disable a notification provider
        /// فعال یا غیرفعال کردن ارائه‌دهنده اعلان
        /// </summary>
        Task SetProviderEnabledAsync(string providerName, bool enabled);
    }

    /// <summary>
    /// Central notification hub implementation
    /// پیاده‌سازی مرکز اعلان‌های مرکزی
    /// </summary>
    public class NotificationHub : INotificationHub
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<NotificationHub> _logger;
        private readonly List<INotificationProvider> _providers;
        private readonly IConfiguration _configuration;

        public NotificationHub(
            ForexDbContext context,
            ILogger<NotificationHub> logger,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _providers = new List<INotificationProvider>();
        }

        public void RegisterProvider(INotificationProvider provider)
        {
            if (!_providers.Any(p => p.ProviderName == provider.ProviderName))
            {
                _providers.Add(provider);
                _logger.LogInformation("Registered notification provider: {ProviderName}", provider.ProviderName);
            }
        }

        public IEnumerable<INotificationProvider> GetProviders() => _providers.AsReadOnly();

        public Task SetProviderEnabledAsync(string providerName, bool enabled)
        {
            // This could be stored in database settings
            var settingKey = $"Notifications:{providerName}:Enabled";
            
            // For now, we'll just log it. In the future, implement database settings
            _logger.LogInformation("Provider {ProviderName} enabled status changed to: {Enabled}", providerName, enabled);
            
            // TODO: Store in SystemSettings table
            // await _settingsService.UpdateSettingAsync(settingKey, enabled.ToString());
            
            return Task.CompletedTask;
        }

        public async Task SendOrderNotificationAsync(Order order, NotificationEventType eventType, string? userId = null, string? oldStatus = null, string? newStatus = null)
        {
            try
            {
                var context = await BuildOrderNotificationContextAsync(order, eventType, userId, oldStatus, newStatus);
                await SendNotificationToProvidersAsync(context, provider => provider.SendOrderNotificationAsync(context));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending order notification for order {OrderId}, event {EventType}", order.Id, eventType);
            }
        }

        public async Task SendAccountingDocumentNotificationAsync(AccountingDocument document, NotificationEventType eventType, string? userId = null)
        {
            try
            {
                var context = await BuildAccountingDocumentNotificationContextAsync(document, eventType, userId);
                await SendNotificationToProvidersAsync(context, provider => provider.SendAccountingDocumentNotificationAsync(context));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending accounting document notification for document {DocumentId}, event {EventType}", document.Id, eventType);
            }
        }

        public async Task SendCustomerNotificationAsync(Customer customer, NotificationEventType eventType, string? userId = null)
        {
            try
            {
                var context = await BuildCustomerNotificationContextAsync(customer, eventType, userId);
                await SendNotificationToProvidersAsync(context, provider => provider.SendCustomerNotificationAsync(context));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending customer notification for customer {CustomerId}, event {EventType}", customer.Id, eventType);
            }
        }

        public async Task SendCustomNotificationAsync(string title, string message, NotificationEventType eventType = NotificationEventType.Custom, string? userId = null, string? navigationUrl = null, NotificationPriority priority = NotificationPriority.Normal)
        {
            try
            {
                // Use explicit URL or default to /admin
                var finalUrl = !string.IsNullOrEmpty(navigationUrl) ? navigationUrl : "/admin";
                _logger.LogInformation("Custom notification URL: {NavigationUrl} -> {FinalUrl}", navigationUrl, finalUrl);

                var context = new NotificationContext
                {
                    EventType = eventType,
                    UserId = userId,
                    Title = title,
                    Message = message,
                    NavigationUrl = finalUrl,
                    Priority = priority,
                    SendToAllAdmins = string.IsNullOrEmpty(userId)
                };

                await SendNotificationToProvidersAsync(context, provider => provider.SendCustomNotificationAsync(context));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending custom notification: {Title}", title);
            }
        }

        private async Task SendNotificationToProvidersAsync(NotificationContext context, Func<INotificationProvider, Task> sendAction)
        {
            var enabledProviders = _providers.Where(p => p.IsEnabled).ToList();
            
            if (!enabledProviders.Any())
            {
                _logger.LogWarning("No enabled notification providers found for event {EventType}", context.EventType);
                return;
            }

            var tasks = enabledProviders.Select(async provider =>
            {
                try
                {
                    await sendAction(provider);
                    _logger.LogDebug("Notification sent via {ProviderName} for event {EventType}", provider.ProviderName, context.EventType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending notification via {ProviderName} for event {EventType}", provider.ProviderName, context.EventType);
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task<NotificationContext> BuildOrderNotificationContextAsync(Order order, NotificationEventType eventType, string? userId, string? oldStatus, string? newStatus)
        {
            var customer = await _context.Customers.FindAsync(order.CustomerId);
            var fromCurrency = await _context.Currencies.FindAsync(order.FromCurrencyId);
            var toCurrency = await _context.Currencies.FindAsync(order.ToCurrencyId);

            var title = eventType switch
            {
                NotificationEventType.OrderCreated => "🔔 سفارش جدید ثبت شد",
                NotificationEventType.OrderUpdated => "🔄 تغییر وضعیت سفارش",
                NotificationEventType.OrderCompleted => "✅ سفارش تکمیل شد",
                NotificationEventType.OrderCancelled => "❌ سفارش لغو شد",
                _ => "📋 رویداد سفارش"
            };

            var message = eventType switch
            {
                NotificationEventType.OrderCreated => $"سفارش #{order.Id} برای {customer?.FullName ?? "نامعلوم"}: {order.Amount:N0} {fromCurrency?.Symbol} → {order.TotalAmount:N0} {toCurrency?.Symbol}",
                NotificationEventType.OrderUpdated when !string.IsNullOrEmpty(oldStatus) && !string.IsNullOrEmpty(newStatus) => $"سفارش #{order.Id} ({customer?.FullName}): {oldStatus} → {newStatus}",
                NotificationEventType.OrderCompleted => $"سفارش #{order.Id} برای {customer?.FullName ?? "نامعلوم"} با موفقیت تکمیل شد",
                NotificationEventType.OrderCancelled => $"سفارش #{order.Id} برای {customer?.FullName ?? "نامعلوم"} لغو شد",
                _ => $"رویداد سفارش #{order.Id}"
            };

            var navigationUrl = $"/Orders/Details/{order.Id}";
            _logger.LogInformation("Order notification URL generated: {NavigationUrl} for order {OrderId}", navigationUrl, order.Id);

            return new NotificationContext
            {
                EventType = eventType,
                UserId = userId,
                Title = title,
                Message = message,
                NavigationUrl = navigationUrl,
                Priority = NotificationPriority.Normal,
                RelatedEntity = new RelatedEntity
                {
                    EntityType = "Order",
                    EntityId = order.Id,
                    EntityData = new Dictionary<string, object>
                    {
                        ["customerId"] = order.CustomerId,
                        ["customerName"] = customer?.FullName ?? "نامعلوم",
                        ["fromCurrencyId"] = order.FromCurrencyId,
                        ["toCurrencyId"] = order.ToCurrencyId,
                        ["amount"] = order.Amount,
                        ["totalAmount"] = order.TotalAmount,
                        ["rate"] = order.Rate
                    }
                },
                Data = new Dictionary<string, object>
                {
                    ["orderId"] = order.Id,
                    ["customerId"] = order.CustomerId,
                    ["amount"] = order.Amount,
                    ["totalAmount"] = order.TotalAmount,
                    ["fromCurrency"] = fromCurrency?.Symbol ?? "",
                    ["toCurrency"] = toCurrency?.Symbol ?? "",
                    ["oldStatus"] = oldStatus ?? "",
                    ["newStatus"] = newStatus ?? ""
                }
            };
        }

        private async Task<NotificationContext> BuildAccountingDocumentNotificationContextAsync(AccountingDocument document, NotificationEventType eventType, string? userId)
        {
            var payerCustomer = await _context.Customers.FindAsync(document.PayerCustomerId);
            var receiverCustomer = await _context.Customers.FindAsync(document.ReceiverCustomerId);
            var currency = await _context.Currencies.FindAsync(document.CurrencyCode);

            var title = eventType switch
            {
                NotificationEventType.AccountingDocumentCreated => "📄 سند حسابداری جدید",
                NotificationEventType.AccountingDocumentVerified => "✅ تأیید سند حسابداری",
                NotificationEventType.AccountingDocumentRejected => "❌ رد سند حسابداری",
                _ => "📋 رویداد سند حسابداری"
            };

            var message = eventType switch
            {
                NotificationEventType.AccountingDocumentCreated => $"{document.Title}: {document.Amount:N0} {currency?.Symbol ?? document.CurrencyCode}",
                NotificationEventType.AccountingDocumentVerified => $"{document.Title}: {document.Amount:N0} {currency?.Symbol ?? document.CurrencyCode} تأیید شد",
                NotificationEventType.AccountingDocumentRejected => $"{document.Title}: {document.Amount:N0} {currency?.Symbol ?? document.CurrencyCode} رد شد",
                _ => $"رویداد سند #{document.Id}"
            };

            if (payerCustomer != null)
            {
                message += $" از {payerCustomer.FullName}";
            }
            if (receiverCustomer != null)
            {
                message += $" به {receiverCustomer.FullName}";
            }

            var navigationUrl = $"/AccountingDocuments/Details/{document.Id}";
            _logger.LogInformation("Document notification URL generated: {NavigationUrl} for document {DocumentId}", navigationUrl, document.Id);

            return new NotificationContext
            {
                EventType = eventType,
                UserId = userId,
                Title = title,
                Message = message,
                NavigationUrl = navigationUrl,
                Priority = eventType == NotificationEventType.AccountingDocumentVerified ? NotificationPriority.High : NotificationPriority.Normal,
                RelatedEntity = new RelatedEntity
                {
                    EntityType = "AccountingDocument",
                    EntityId = document.Id,
                    EntityData = new Dictionary<string, object>
                    {
                        ["payerCustomerId"] = document.PayerCustomerId ?? 0,
                        ["receiverCustomerId"] = document.ReceiverCustomerId ?? 0,
                        ["amount"] = document.Amount,
                        ["currencyCode"] = document.CurrencyCode,
                        ["title"] = document.Title
                    }
                },
                Data = new Dictionary<string, object>
                {
                    ["documentId"] = document.Id,
                    ["payerCustomerId"] = document.PayerCustomerId ?? 0,
                    ["receiverCustomerId"] = document.ReceiverCustomerId ?? 0,
                    ["amount"] = document.Amount,
                    ["currencyCode"] = document.CurrencyCode,
                    ["title"] = document.Title,
                    ["isVerified"] = document.IsVerified
                }
            };
        }

        private Task<NotificationContext> BuildCustomerNotificationContextAsync(Customer customer, NotificationEventType eventType, string? userId)
        {
            var title = eventType switch
            {
                NotificationEventType.CustomerRegistered => "👤 مشتری جدید ثبت شد",
                NotificationEventType.CustomerBalanceChanged => "💰 تغییر موجودی مشتری",
                NotificationEventType.CustomerStatusChanged => "🔄 تغییر وضعیت مشتری",
                _ => "👤 رویداد مشتری"
            };

            var message = eventType switch
            {
                NotificationEventType.CustomerRegistered => $"مشتری جدید: {customer.FullName} ({customer.PhoneNumber})",
                NotificationEventType.CustomerBalanceChanged => $"موجودی مشتری {customer.FullName} تغییر کرد",
                NotificationEventType.CustomerStatusChanged => $"وضعیت مشتری {customer.FullName} تغییر کرد",
                _ => $"رویداد مشتری {customer.FullName}"
            };

            var navigationUrl = $"/Customers/Details/{customer.Id}";
            _logger.LogInformation("Customer notification URL generated: {NavigationUrl} for customer {CustomerId}", navigationUrl, customer.Id);

            return Task.FromResult(new NotificationContext
            {
                EventType = eventType,
                UserId = userId,
                Title = title,
                Message = message,
                NavigationUrl = navigationUrl,
                Priority = NotificationPriority.Normal,
                RelatedEntity = new RelatedEntity
                {
                    EntityType = "Customer",
                    EntityId = customer.Id,
                    EntityData = new Dictionary<string, object>
                    {
                        ["fullName"] = customer.FullName,
                        ["phoneNumber"] = customer.PhoneNumber,
                        ["isActive"] = customer.IsActive
                    }
                },
                Data = new Dictionary<string, object>
                {
                    ["customerId"] = customer.Id,
                    ["fullName"] = customer.FullName,
                    ["phoneNumber"] = customer.PhoneNumber,
                    ["isActive"] = customer.IsActive
                }
            });
        }
    }
}
