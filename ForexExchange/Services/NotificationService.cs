using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    public interface INotificationService
    {
        Task SendOrderCreatedNotificationAsync(Order order);
        Task SendOrderMatchedNotificationAsync(Transaction transaction);
        Task SendTransactionStatusNotificationAsync(Transaction transaction);
        Task SendReceiptUploadedNotificationAsync(Receipt receipt);
        Task SendSystemAlertAsync(string message, NotificationPriority priority = NotificationPriority.Normal);
        Task<List<Notification>> GetUserNotificationsAsync(int customerId, bool unreadOnly = false);
        Task MarkNotificationAsReadAsync(int notificationId);
        Task MarkAllNotificationsAsReadAsync(int customerId);
    }

    public class NotificationService : INotificationService
    {
        private readonly ForexDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ForexDbContext context,
            IEmailService emailService,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task SendOrderCreatedNotificationAsync(Order order)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(order.CustomerId);
                if (customer == null) return;

                var message = $"سفارش {GetOrderTypeText(order.OrderType)} شما برای {order.Amount:N0} {GetCurrencyText(order.Currency)} " +
                             $"با نرخ {order.Rate:N0} تومان ثبت شد.";

                await CreateNotificationAsync(
                    customerId: order.CustomerId,
                    title: "سفارش جدید ثبت شد",
                    message: message,
                    type: NotificationType.OrderCreated,
                    relatedEntityId: order.Id,
                    priority: NotificationPriority.Normal
                );

                // Send email notification
                await _emailService.SendEmailAsync(
                    customer.Email,
                    "تأیید ثبت سفارش",
                    $"سلام {customer.FullName}،\n\n{message}\n\nبا تشکر،\nسیستم صرافی"
                );

                _logger.LogInformation("Order created notification sent for order {OrderId}", order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send order created notification for order {OrderId}", order.Id);
            }
        }

        public async Task SendOrderMatchedNotificationAsync(Transaction transaction)
        {
            try
            {
                var buyer = await _context.Customers.FindAsync(transaction.BuyerCustomerId);
                var seller = await _context.Customers.FindAsync(transaction.SellerCustomerId);

                if (buyer != null)
                {
                    var buyerMessage = $"سفارش خرید شما با فروشنده ای تطبیق یافت. " +
                                     $"مبلغ: {transaction.Amount:N2} {GetCurrencyText(transaction.Currency)} " +
                                     $"به نرخ {transaction.Rate:N0} تومان. " +
                                     $"کل قابل پرداخت: {transaction.TotalInToman:N0} تومان.";

                    await CreateNotificationAsync(
                        customerId: transaction.BuyerCustomerId,
                        title: "تطبیق سفارش خرید",
                        message: buyerMessage,
                        type: NotificationType.OrderMatched,
                        relatedEntityId: transaction.Id,
                        priority: NotificationPriority.High
                    );

                    await _emailService.SendEmailAsync(buyer.Email, "تطبیق سفارش خرید", buyerMessage);
                }

                if (seller != null)
                {
                    var sellerMessage = $"سفارش فروش شما با خریداری تطبیق یافت. " +
                                      $"مبلغ: {transaction.Amount:N2} {GetCurrencyText(transaction.Currency)} " +
                                      $"به نرخ {transaction.Rate:N0} تومان. " +
                                      $"کل دریافتی: {transaction.TotalInToman:N0} تومان.";

                    await CreateNotificationAsync(
                        customerId: transaction.SellerCustomerId,
                        title: "تطبیق سفارش فروش",
                        message: sellerMessage,
                        type: NotificationType.OrderMatched,
                        relatedEntityId: transaction.Id,
                        priority: NotificationPriority.High
                    );

                    await _emailService.SendEmailAsync(seller.Email, "تطبیق سفارش فروش", sellerMessage);
                }

                _logger.LogInformation("Order matched notifications sent for transaction {TransactionId}", transaction.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send order matched notifications for transaction {TransactionId}", transaction.Id);
            }
        }

        public async Task SendTransactionStatusNotificationAsync(Transaction transaction)
        {
            try
            {
                var buyer = await _context.Customers.FindAsync(transaction.BuyerCustomerId);
                var seller = await _context.Customers.FindAsync(transaction.SellerCustomerId);

                var (buyerMessage, sellerMessage, priority) = GetStatusMessages(transaction);

                if (buyer != null && !string.IsNullOrEmpty(buyerMessage))
                {
                    await CreateNotificationAsync(
                        customerId: transaction.BuyerCustomerId,
                        title: "تغییر وضعیت تراکنش",
                        message: buyerMessage,
                        type: NotificationType.TransactionStatusChanged,
                        relatedEntityId: transaction.Id,
                        priority: priority
                    );

                    await _emailService.SendEmailAsync(buyer.Email, "تغییر وضعیت تراکنش", buyerMessage);
                }

                if (seller != null && !string.IsNullOrEmpty(sellerMessage))
                {
                    await CreateNotificationAsync(
                        customerId: transaction.SellerCustomerId,
                        title: "تغییر وضعیت تراکنش",
                        message: sellerMessage,
                        type: NotificationType.TransactionStatusChanged,
                        relatedEntityId: transaction.Id,
                        priority: priority
                    );

                    await _emailService.SendEmailAsync(seller.Email, "تغییر وضعیت تراکنش", sellerMessage);
                }

                _logger.LogInformation("Transaction status notifications sent for transaction {TransactionId}", transaction.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send transaction status notifications for transaction {TransactionId}", transaction.Id);
            }
        }

        public async Task SendReceiptUploadedNotificationAsync(Receipt receipt)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(receipt.CustomerId);
                if (customer == null) return;

                var message = $"رسید شما با موفقیت آپلود شد و در حال بررسی است. " +
                             $"نوع رسید: {GetReceiptTypeText(receipt.Type)}";

                await CreateNotificationAsync(
                    customerId: receipt.CustomerId,
                    title: "آپلود رسید",
                    message: message,
                    type: NotificationType.ReceiptUploaded,
                    relatedEntityId: receipt.Id,
                    priority: NotificationPriority.Normal
                );

                await _emailService.SendEmailAsync(customer.Email, "تأیید آپلود رسید", message);

                _logger.LogInformation("Receipt uploaded notification sent for receipt {ReceiptId}", receipt.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send receipt uploaded notification for receipt {ReceiptId}", receipt.Id);
            }
        }

        public async Task SendSystemAlertAsync(string message, NotificationPriority priority = NotificationPriority.Normal)
        {
            try
            {
                // Send to all active customers
                var activeCustomers = await _context.Customers
                    .Where(c => c.IsActive)
                    .ToListAsync();

                foreach (var customer in activeCustomers)
                {
                    await CreateNotificationAsync(
                        customerId: customer.Id,
                        title: "اطلاعیه سیستم",
                        message: message,
                        type: NotificationType.SystemAlert,
                        relatedEntityId: null,
                        priority: priority
                    );

                    if (priority == NotificationPriority.Critical)
                    {
                        await _emailService.SendEmailAsync(customer.Email, "اطلاعیه مهم سیستم", message);
                    }
                }

                _logger.LogInformation("System alert sent to {CustomerCount} customers", activeCustomers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send system alert");
            }
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(int customerId, bool unreadOnly = false)
        {
            var query = _context.Notifications
                .Where(n => n.CustomerId == customerId);

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(50) // Limit to latest 50 notifications
                .ToListAsync();
        }

        public async Task MarkNotificationAsReadAsync(int notificationId)
        {
            try
            {
                var notification = await _context.Notifications.FindAsync(notificationId);
                if (notification != null && !notification.IsRead)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark notification {NotificationId} as read", notificationId);
            }
        }

        public async Task MarkAllNotificationsAsReadAsync(int customerId)
        {
            try
            {
                var unreadNotifications = await _context.Notifications
                    .Where(n => n.CustomerId == customerId && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark all notifications as read for customer {CustomerId}", customerId);
            }
        }

        private async Task CreateNotificationAsync(
            int customerId,
            string title,
            string message,
            NotificationType type,
            int? relatedEntityId,
            NotificationPriority priority)
        {
            var notification = new Notification
            {
                CustomerId = customerId,
                Title = title,
                Message = message,
                Type = type,
                RelatedEntityId = relatedEntityId,
                Priority = priority,
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        private (string buyerMessage, string sellerMessage, NotificationPriority priority) GetStatusMessages(Transaction transaction)
        {
            return transaction.Status switch
            {
                TransactionStatus.PaymentUploaded => (
                    "لطفاً رسید پرداخت خود را آپلود کنید تا فرآیند تسویه ادامه یابد.",
                    "منتظر آپلود رسید پرداخت از طرف خریدار هستیم.",
                    NotificationPriority.High
                ),
                TransactionStatus.ReceiptConfirmed => (
                    "رسید پرداخت شما تأیید شد. منتظر انتقال ارز از طرف فروشنده باشید.",
                    "پرداخت خریدار تأیید شد. لطفاً انتقال ارز را انجام دهید.",
                    NotificationPriority.High
                ),
                TransactionStatus.Completed => (
                    "تراکنش شما با موفقیت تکمیل شد.",
                    "تراکنش شما با موفقیت تکمیل شد.",
                    NotificationPriority.Normal
                ),
                TransactionStatus.Failed => (
                    "متأسفانه تراکنش شما ناموفق بود.",
                    "متأسفانه تراکنش شما ناموفق بود.",
                    NotificationPriority.High
                ),
                _ => ("", "", NotificationPriority.Normal)
            };
        }

        private string GetOrderTypeText(OrderType orderType)
        {
            return orderType switch
            {
                OrderType.Buy => "خرید",
                OrderType.Sell => "فروش",
                _ => orderType.ToString()
            };
        }

        private string GetCurrencyText(CurrencyType currency)
        {
            return currency switch
            {
                CurrencyType.USD => "دلار آمریکا",
                CurrencyType.EUR => "یورو",
                CurrencyType.AED => "درهم امارات",
                CurrencyType.OMR => "ریال عمان",
                CurrencyType.TRY => "لیر ترکیه",
                _ => currency.ToString()
            };
        }

        private string GetReceiptTypeText(ReceiptType receiptType)
        {
            return receiptType switch
            {
                ReceiptType.PaymentReceipt => "رسید پرداخت",
                ReceiptType.BankStatement => "گردش حساب",
                _ => receiptType.ToString()
            };
        }
    }
}
