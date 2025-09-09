using Microsoft.AspNetCore.SignalR;
using ForexExchange.Hubs;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;

namespace ForexExchange.Services
{
    /// <summary>
    /// Service for sending real-time notifications to admin users using SignalR
    /// Enhanced with comprehensive notification methods for all admin events
    /// </summary>
    public class AdminNotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminNotificationService(
            IHubContext<NotificationHub> hubContext,
            UserManager<ApplicationUser> userManager)
        {
            _hubContext = hubContext;
            _userManager = userManager;
        }

        #region Core Notification Methods

        /// <summary>
        /// Send notification to all admin users (Admin, Manager, Staff roles)
        /// </summary>
        public async Task SendToAllAdminsAsync(string title, string message, string type = "info", object? data = null)
        {
            await _hubContext.Clients.Groups("Admin", "Manager", "Staff")
                .SendAsync("ReceiveNotification", new
                {
                    title = title,
                    message = message,
                    type = type,
                    timestamp = DateTime.Now,
                    data = data
                });
        }

        /// <summary>
        /// Send notification to specific user by user ID
        /// </summary>
        public async Task SendToUserAsync(string userId, string title, string message, string type = "info", object? data = null)
        {
            await _hubContext.Clients.User(userId)
                .SendAsync("ReceiveNotification", new
                {
                    title = title,
                    message = message,
                    type = type,
                    timestamp = DateTime.Now,
                    data = data
                });
        }

        #endregion

        #region Order Notifications

        /// <summary>
        /// Send notification when an order is created, updated, cancelled, or completed
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
                action = action,
                amount = order.Amount,
                fromCurrency = order.FromCurrency?.Code,
                toCurrency = order.ToCurrency?.Code,
                timestamp = DateTime.Now
            });
        }

        #endregion

        #region Exchange Rate Notifications

        /// <summary>
        /// Send notification when exchange rates are updated
        /// </summary>
        public async Task SendExchangeRateNotificationAsync(ExchangeRate exchangeRate, string action, string? oldRate = null)
        {
            var title = GetExchangeRateNotificationTitle(action);
            var message = GetExchangeRateNotificationMessage(exchangeRate, action, oldRate);
            var type = GetExchangeRateNotificationType(action);

            await SendToAllAdminsAsync(title, message, type, new
            {
                exchangeRateId = exchangeRate.Id,
                currencyPair = exchangeRate.CurrencyPair,
                newRate = exchangeRate.Rate,
                oldRate = oldRate,
                action = action,
                timestamp = DateTime.Now
            });
        }

        #endregion

        #region User Management Notifications

        /// <summary>
        /// Send notification for user management actions (create, update, delete, role changes)
        /// </summary>
        public async Task SendUserManagementNotificationAsync(string action, string targetUserName, string performedBy)
        {
            var title = action switch
            {
                "created" => "کاربر جدید",
                "updated" => "کاربر بروزرسانی شد",
                "deleted" => "کاربر حذف شد",
                "role_changed" => "نقش کاربر تغییر یافت",
                _ => "اعلان کاربر"
            };

            var message = GetUserManagementMessage(action, targetUserName, performedBy);
            var type = action == "deleted" ? "warning" : "info";

            await SendToAllAdminsAsync(title, message, type, new
            {
                action = action,
                targetUser = targetUserName,
                performedBy = performedBy,
                timestamp = DateTime.Now
            });
        }

        #endregion

        #region Document Notifications

        /// <summary>
        /// Send notification when accounting documents are created, updated, or deleted
        /// </summary>
        public async Task SendDocumentNotificationAsync(AccountingDocument document, string action)
        {
            var title = GetDocumentNotificationTitle(action);
            var message = GetDocumentNotificationMessage(document, action);
            var type = GetDocumentNotificationType(action);

            await SendToAllAdminsAsync(title, message, type, new
            {
                documentId = document.Id,
                documentType = document.Type.ToString(),
                amount = document.Amount,
                currencyCode = document.CurrencyCode,
                payerCustomerId = document.PayerCustomerId,
                receiverCustomerId = document.ReceiverCustomerId,
                action = action,
                timestamp = DateTime.Now
            });
        }

        #endregion

        #region Customer Notifications

        /// <summary>
        /// Send notification when customers are created, updated, or deleted
        /// </summary>
        public async Task SendCustomerNotificationAsync(Customer customer, string action)
        {
            var title = GetCustomerNotificationTitle(action);
            var message = GetCustomerNotificationMessage(customer, action);
            var type = GetCustomerNotificationType(action);

            await SendToAllAdminsAsync(title, message, type, new
            {
                customerId = customer.Id,
                customerName = customer.FullName,
                nationalId = customer.NationalId,
                phoneNumber = customer.PhoneNumber,
                action = action,
                timestamp = DateTime.Now
            });
        }

        #endregion

        #region Bank Account Notifications

        /// <summary>
        /// Send notification when bank accounts are created, updated, or deleted
        /// </summary>
        public async Task SendBankAccountNotificationAsync(BankAccount bankAccount, string action)
        {
            var title = GetBankAccountNotificationTitle(action);
            var message = GetBankAccountNotificationMessage(bankAccount, action);
            var type = GetBankAccountNotificationType(action);

            await SendToAllAdminsAsync(title, message, type, new
            {
                bankAccountId = bankAccount.Id,
                bankName = bankAccount.BankName,
                accountNumber = bankAccount.AccountNumber,
                currencyCode = bankAccount.CurrencyCode,
                action = action,
                timestamp = DateTime.Now
            });
        }

        #endregion

        #region Pool Balance Notifications

        /// <summary>
        /// Send notification when currency pool balances are updated
        /// </summary>
        public async Task SendPoolBalanceNotificationAsync(CurrencyPool pool, decimal oldBalance, decimal newBalance, string action = "balance_updated")
        {
            var title = GetPoolBalanceNotificationTitle(action);
            var message = GetPoolBalanceNotificationMessage(pool, oldBalance, newBalance, action);
            var type = GetPoolBalanceNotificationType(action);

            await SendToAllAdminsAsync(title, message, type, new
            {
                poolId = pool.Id,
                currency = pool.Currency?.Code,
                oldBalance = oldBalance,
                newBalance = newBalance,
                difference = newBalance - oldBalance,
                timestamp = DateTime.Now
            });
        }

        #endregion

        #region Bulk Operations Notifications

        /// <summary>
        /// Send notification for bulk operations
        /// </summary>
        public async Task SendBulkOperationNotificationAsync(string operationType, int recordCount, string details = "")
        {
            var title = $"عملیات گروهی: {operationType}";
            var message = string.IsNullOrEmpty(details) 
                ? $"عملیات {operationType} بر روی {recordCount} رکورد انجام شد"
                : $"{details}\nتعداد رکوردها: {recordCount}";

            await SendToAllAdminsAsync(title, message, "info", new
            {
                operationType = operationType,
                recordCount = recordCount,
                details = details,
                timestamp = DateTime.Now
            });
        }

        #endregion

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

        private string GetDocumentNotificationTitle(string action)
        {
            return action switch
            {
                "created" => "سند جدید",
                "updated" => "سند بروزرسانی شد",
                "deleted" => "سند حذف شد",
                _ => "اعلان سند"
            };
        }

        private string GetDocumentNotificationMessage(AccountingDocument document, string action)
        {
            var customerName = document.PayerCustomer?.FullName ?? document.ReceiverCustomer?.FullName ?? "مشتری ناشناس";
            var currencyCode = document.CurrencyCode ?? "ارز نامشخص";

            return action switch
            {
                "created" => $"سند حسابداری جدید ثبت شد\nمشتری: {customerName}\nنوع سند: {document.Type}\nمبلغ: {document.Amount:N0} {currencyCode}",
                "updated" => $"سند #{document.Id} بروزرسانی شد\nمشتری: {customerName}\nمبلغ: {document.Amount:N0} {currencyCode}",
                "deleted" => $"سند #{document.Id} حذف شد\nمشتری: {customerName}",
                _ => $"سند #{document.Id} تغییر یافت."
            };
        }

        private string GetDocumentNotificationType(string action)
        {
            return action switch
            {
                "created" => "success",
                "updated" => "info",
                "deleted" => "warning",
                _ => "info"
            };
        }

        private string GetCustomerNotificationTitle(string action)
        {
            return action switch
            {
                "created" => "مشتری جدید",
                "updated" => "مشتری بروزرسانی شد",
                "deleted" => "مشتری حذف شد",
                _ => "اعلان مشتری"
            };
        }

        private string GetCustomerNotificationMessage(Customer customer, string action)
        {
            return action switch
            {
                "created" => $"مشتری جدید ثبت شد\nنام: {customer.FullName}\nکد ملی: {customer.NationalId}\nتلفن: {customer.PhoneNumber}",
                "updated" => $"اطلاعات مشتری {customer.FullName} بروزرسانی شد",
                "deleted" => $"مشتری {customer.FullName} حذف شد",
                _ => $"مشتری {customer.FullName} تغییر یافت."
            };
        }

        private string GetCustomerNotificationType(string action)
        {
            return action switch
            {
                "created" => "success",
                "updated" => "info",
                "deleted" => "warning",
                _ => "info"
            };
        }

        private string GetBankAccountNotificationTitle(string action)
        {
            return action switch
            {
                "created" => "حساب بانکی جدید",
                "updated" => "حساب بانکی بروزرسانی شد",
                "deleted" => "حساب بانکی حذف شد",
                _ => "اعلان حساب بانکی"
            };
        }

        private string GetBankAccountNotificationMessage(BankAccount bankAccount, string action)
        {
            var currencyCode = bankAccount.CurrencyCode ?? "ارز نامشخص";

            return action switch
            {
                "created" => $"حساب بانکی جدید ثبت شد\nبانک: {bankAccount.BankName}\nشماره حساب: {bankAccount.AccountNumber}\nارز: {currencyCode}",
                "updated" => $"حساب بانکی بروزرسانی شد\nبانک: {bankAccount.BankName}\nشماره حساب: {bankAccount.AccountNumber}",
                "deleted" => $"حساب بانکی حذف شد\nبانک: {bankAccount.BankName}\nشماره حساب: {bankAccount.AccountNumber}",
                _ => $"حساب بانکی {bankAccount.AccountNumber} تغییر یافت."
            };
        }

        private string GetBankAccountNotificationType(string action)
        {
            return action switch
            {
                "created" => "success",
                "updated" => "info",
                "deleted" => "warning",
                _ => "info"
            };
        }

        private string GetPoolBalanceNotificationTitle(string action)
        {
            return action switch
            {
                "balance_updated" => "موجودی استخر بروزرسانی شد",
                "balance_increased" => "موجودی استخر افزایش یافت",
                "balance_decreased" => "موجودی استخر کاهش یافت",
                _ => "اعلان موجودی استخر"
            };
        }

        private string GetPoolBalanceNotificationMessage(CurrencyPool pool, decimal oldBalance, decimal newBalance, string action)
        {
            var currencyCode = pool.Currency?.Code ?? "ارز نامشخص";
            var difference = newBalance - oldBalance;

            return action switch
            {
                "balance_updated" => $"موجودی استخر {currencyCode} بروزرسانی شد\nموجودی قبلی: {oldBalance:N0}\nموجودی جدید: {newBalance:N0}\nتغییر: {difference:N0}",
                "balance_increased" => $"موجودی استخر {currencyCode} افزایش یافت\nمقدار افزایش: {difference:N0}\nموجودی فعلی: {newBalance:N0}",
                "balance_decreased" => $"موجودی استخر {currencyCode} کاهش یافت\nمقدار کاهش: {Math.Abs(difference):N0}\nموجودی فعلی: {newBalance:N0}",
                _ => $"موجودی استخر {currencyCode} تغییر یافت."
            };
        }

        private string GetPoolBalanceNotificationType(string action)
        {
            return action switch
            {
                "balance_increased" => "success",
                "balance_decreased" => "warning",
                "balance_updated" => "info",
                _ => "info"
            };
        }

        #endregion
    }
}
