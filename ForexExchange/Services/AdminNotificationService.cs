using Microsoft.AspNetCore.SignalR;
using ForexExchange.Hubs;
using ForexExchange.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;

namespace ForexExchange.Services
{
    /// <summary>
    /// Admin Notification Service for real-time notifications
    /// سرویس اعلان ادمین برای اعلان‌های بلادرنگ
    /// </summary>
    public class AdminNotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AdminActivityService _adminActivityService;

        public AdminNotificationService(
            IHubContext<NotificationHub> hubContext,
            UserManager<ApplicationUser> userManager,
            AdminActivityService adminActivityService)
        {
            _hubContext = hubContext;
            _userManager = userManager;
            _adminActivityService = adminActivityService;
        }

        /// <summary>
        /// Send notification to all admin users
        /// ارسال اعلان به تمام کاربران ادمین
        /// </summary>
        public async Task SendToAllAdminsAsync(string title, string message, string type = "info", object? data = null)
        {
            var notification = new
            {
                title = title,
                message = message,
                type = type,
                data = data,
                timestamp = DateTime.Now
            };

            await _hubContext.Clients.Group("Admins").SendAsync("ReceiveNotification", notification);
        }

        /// <summary>
        /// Send notification to all super admin users
        /// ارسال اعلان به تمام کاربران سوپر ادمین
        /// </summary>
        public async Task SendToSuperAdminsAsync(string title, string message, string type = "info", object? data = null)
        {
            var notification = new
            {
                title = title,
                message = message,
                type = type,
                data = data,
                timestamp = DateTime.Now
            };

            await _hubContext.Clients.Group("SuperAdmins").SendAsync("ReceiveNotification", notification);
        }

        /// <summary>
        /// Send notification to a specific user
        /// ارسال اعلان به کاربر خاص
        /// </summary>
        public async Task SendToUserAsync(string userId, string title, string message, string type = "info", object? data = null)
        {
            var notification = new
            {
                title = title,
                message = message,
                type = type,
                data = data,
                timestamp = DateTime.Now
            };

            await _hubContext.Clients.Group($"User_{userId}").SendAsync("ReceiveNotification", notification);
        }

        /// <summary>
        /// Send order-related notification to admins
        /// ارسال اعلان مرتبط با معامله به ادمین‌ها
        /// </summary>
        public async Task SendOrderNotificationAsync(Order order, string action)
        {
            var title = GetOrderNotificationTitle(action);
            var message = GetOrderNotificationMessage(order, action);
            var type = GetOrderNotificationType(action);

            await SendToAllAdminsAsync(title, message, type, new
            {
                orderId = order.Id,
                customerId = order.CustomerId,
                customerName = order.Customer?.FullName,
                amount = order.Amount,
                currency = order.FromCurrency?.Code,
                rate = order.Rate,
                totalAmount = order.TotalAmount,
                status = order.Status.ToString()
            });
        }

        /// <summary>
        /// Send exchange rate notification to admins
        /// ارسال اعلان نرخ ارز به ادمین‌ها
        /// </summary>
        public async Task SendExchangeRateNotificationAsync(ExchangeRate exchangeRate, string action, string? oldRate = null)
        {
            var title = GetExchangeRateNotificationTitle(action);
            var message = GetExchangeRateNotificationMessage(exchangeRate, action, oldRate);
            var type = GetExchangeRateNotificationType(action);

            await SendToAllAdminsAsync(title, message, type, new
            {
                exchangeRateId = exchangeRate.Id,
                fromCurrency = exchangeRate.FromCurrency?.Code,
                toCurrency = exchangeRate.ToCurrency?.Code,
                currencyPair = exchangeRate.CurrencyPair,
                newRate = exchangeRate.Rate,
                oldRate = oldRate,
                updatedAt = exchangeRate.UpdatedAt
            });
        }

        /// <summary>
        /// Send system alert to all admins
        /// ارسال هشدار سیستمی به تمام ادمین‌ها
        /// </summary>
        public async Task SendSystemAlertAsync(string message, string severity = "warning")
        {
            var title = "هشدار سیستمی";
            var type = severity switch
            {
                "error" => "error",
                "success" => "success",
                "info" => "info",
                _ => "warning"
            };

            await SendToAllAdminsAsync(title, message, type);
        }

        /// <summary>
        /// Send security alert to super admins
        /// ارسال هشدار امنیتی به سوپر ادمین‌ها
        /// </summary>
        public async Task SendSecurityAlertAsync(string message, string? details = null)
        {
            var title = "هشدار امنیتی";
            var fullMessage = string.IsNullOrEmpty(details) ? message : $"{message}\n{details}";

            await SendToSuperAdminsAsync(title, fullMessage, "error", new
            {
                alertType = "security",
                details = details,
                timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Send user management notification
        /// ارسال اعلان مدیریت کاربر
        /// </summary>
        public async Task SendUserManagementNotificationAsync(string action, string targetUserName, string performedBy)
        {
            var title = "مدیریت کاربران";
            var message = GetUserManagementMessage(action, targetUserName, performedBy);
            var type = "info";

            await SendToSuperAdminsAsync(title, message, type, new
            {
                action = action,
                targetUser = targetUserName,
                performedBy = performedBy,
                timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Send bulk operation notification
        /// ارسال اعلان عملیات انبوه
        /// </summary>
        public async Task SendBulkOperationNotificationAsync(string operation, int affectedCount, string? details = null)
        {
            var title = "عملیات انبوه";
            var message = $"عملیات {operation} با موفقیت انجام شد. تعداد موارد تاثیرپذیر: {affectedCount}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $"\n{details}";
            }

            await SendToAllAdminsAsync(title, message, "success", new
            {
                operation = operation,
                affectedCount = affectedCount,
                details = details,
                timestamp = DateTime.Now
            });
        }

        #region Helper Methods

        private string GetOrderNotificationTitle(string action)
        {
            return action switch
            {
                "created" => "معامله جدید",
                "updated" => "معامله بروزرسانی شد",
                "cancelled" => "معامله لغو شد",
                "completed" => "معامله تکمیل شد",
                _ => "اعلان معامله"
            };
        }

        private string GetOrderNotificationMessage(Order order, string action)
        {
            var customerName = order.Customer?.FullName ?? "مشتری ناشناس";
            var fromCurrency = order.FromCurrency?.Code ?? "ارز نامشخص";
            var toCurrency = order.ToCurrency?.Code ?? "ارز نامشخص";

            return action switch
            {
                "created" => $"معامله جدید ثبت شد\nمشتری: {customerName}\nمبلغ: {order.Amount:N0}\nارز مبدا: {fromCurrency}\nارز مقصد: {toCurrency}",
                "updated" => $"معامله #{order.Id} از {customerName} بروزرسانی شد.",
                "cancelled" => $"معامله #{order.Id} از {customerName} لغو شد.",
                "completed" => $"معامله #{order.Id} از {customerName} تکمیل شد.",
                _ => $"معامله #{order.Id} تغییر وضعیت یافت."
            };
        }

        private string GetOrderNotificationType(string action)
        {
            return action switch
            {
                "created" => "success",
                "updated" => "info",
                "cancelled" => "warning",
                "completed" => "success",
                _ => "info"
            };
        }

        private string GetExchangeRateNotificationTitle(string action)
        {
            return action switch
            {
                "updated" => "نرخ ارز بروزرسانی شد",
                "created" => "نرخ ارز جدید",
                "deleted" => "نرخ ارز حذف شد",
                _ => "اعلان نرخ ارز"
            };
        }

        private string GetExchangeRateNotificationMessage(ExchangeRate exchangeRate, string action, string? oldRate)
        {
            var pair = exchangeRate.CurrencyPair;

            return action switch
            {
                "updated" => string.IsNullOrEmpty(oldRate)
                    ? $"نرخ ارز {pair} به {exchangeRate.Rate:N0} بروزرسانی شد."
                    : $"نرخ ارز {pair} از {oldRate} به {exchangeRate.Rate:N0} تغییر یافت.",
                "created" => $"نرخ ارز جدید {pair} با نرخ {exchangeRate.Rate:N0} اضافه شد.",
                "deleted" => $"نرخ ارز {pair} حذف شد.",
                _ => $"نرخ ارز {pair} تغییر یافت."
            };
        }

        private string GetExchangeRateNotificationType(string action)
        {
            return action switch
            {
                "updated" => "info",
                "created" => "success",
                "deleted" => "warning",
                _ => "info"
            };
        }

        private string GetUserManagementMessage(string action, string targetUserName, string performedBy)
        {
            return action switch
            {
                "created" => $"کاربر جدید {targetUserName} توسط {performedBy} ایجاد شد.",
                "updated" => $"کاربر {targetUserName} توسط {performedBy} بروزرسانی شد.",
                "deleted" => $"کاربر {targetUserName} توسط {performedBy} حذف شد.",
                "role_changed" => $"نقش کاربر {targetUserName} توسط {performedBy} تغییر یافت.",
                _ => $"کاربر {targetUserName} توسط {performedBy} تغییر یافت."
            };
        }

        #endregion
    }
}
