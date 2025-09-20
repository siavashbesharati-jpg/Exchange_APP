using ForexExchange.Models;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ForexExchange.Services
{
    /// <summary>
    /// Admin Activity Logging Service
    /// سرویس لاگ فعالیت‌های ادمین
    /// </summary>
    public class AdminActivityService
    {
        private readonly ForexDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminActivityService(ForexDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Log an admin activity
        /// لاگ کردن فعالیت ادمین
        /// </summary>
        public async Task LogActivityAsync(
            string adminUserId,
            string adminUsername,
            AdminActivityType activityType,
            string description,
            string? details = null,
            string? entityType = null,
            int? entityId = null,
            string? oldValue = null,
            string? newValue = null,
            bool isSuccess = true)
        {
            try
            {
                var activity = new AdminActivity
                {
                    AdminUserId = adminUserId,
                    AdminUsername = adminUsername,
                    ActivityType = activityType,
                    Description = description,
                    Details = details,
                    EntityType = entityType,
                    EntityId = entityId,
                    OldValue = oldValue,
                    NewValue = newValue,
                    IsSuccess = isSuccess,
                    Timestamp = DateTime.Now
                };

                // Get IP address and user agent from current request
                if (_httpContextAccessor.HttpContext != null)
                {
                    activity.IpAddress = GetClientIpAddress();
                    activity.UserAgent = _httpContextAccessor.HttpContext.Request.Headers["User-Agent"].ToString();
                }

                _context.AdminActivities.Add(activity);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log the error but don't throw it to avoid breaking the main operation
                Console.WriteLine($"Error logging admin activity: {ex.Message}");
            }
        }

        /// <summary>
        /// Log order creation activity
        /// لاگ کردن فعالیت ایجاد معامله
        /// </summary>
        public async Task LogOrderCreatedAsync(Order order, string adminUserId, string adminUsername)
        {
            var details = new
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                FromCurrencyId = order.FromCurrencyId,
                ToCurrencyId = order.ToCurrencyId,
                Amount = order.FromAmount,
                Rate = order.Rate,
                TotalAmount = order.ToAmount
            };

            await LogActivityAsync(
                adminUserId,
                adminUsername,
                AdminActivityType.OrderCreated,
                $"معامله #{order.Id} برای مشتری {order.Customer?.FullName} ایجاد شد",
                JsonSerializer.Serialize(details),
                "Order",
                order.Id
            );
        }

        /// <summary>
        /// Log order update activity
        /// لاگ کردن فعالیت بروزرسانی معامله
        /// </summary>
        public async Task LogOrderUpdatedAsync(Order order, string adminUserId, string adminUsername, string? oldValues = null)
        {
            var details = new
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                Status = "N/A",
                UpdatedAt = order.UpdatedAt
            };

            await LogActivityAsync(
                adminUserId,
                adminUsername,
                AdminActivityType.OrderUpdated,
                $"معامله #{order.Id} بروزرسانی شد",
                JsonSerializer.Serialize(details),
                "Order",
                order.Id,
                oldValues
            );
        }

        /// <summary>
        /// Log order cancellation activity
        /// لاگ کردن فعالیت لغو معامله
        /// </summary>
        public async Task LogOrderCancelledAsync(Order order, string adminUserId, string adminUsername)
        {
            var details = new
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                CancelledAt = DateTime.Now
            };

            await LogActivityAsync(
                adminUserId,
                adminUsername,
                AdminActivityType.OrderCancelled,
                $"معامله #{order.Id} لغو شد",
                JsonSerializer.Serialize(details),
                "Order",
                order.Id
            );
        }

        /// <summary>
        /// Log exchange rate update activity
        /// لاگ کردن فعالیت بروزرسانی نرخ ارز
        /// </summary>
        public async Task LogExchangeRateUpdatedAsync(ExchangeRate exchangeRate, string adminUserId, string adminUsername, string? oldRate = null)
        {
            var details = new
            {
                ExchangeRateId = exchangeRate.Id,
                FromCurrency = exchangeRate.FromCurrency?.Code,
                ToCurrency = exchangeRate.ToCurrency?.Code,
                NewRate = exchangeRate.Rate,
                OldRate = oldRate
            };

            await LogActivityAsync(
                adminUserId,
                adminUsername,
                AdminActivityType.ExchangeRateUpdated,
                $"نرخ ارز {exchangeRate.CurrencyPair} بروزرسانی شد",
                JsonSerializer.Serialize(details),
                "ExchangeRate",
                exchangeRate.Id,
                oldRate,
                exchangeRate.Rate.ToString()
            );
        }

        /// <summary>
        /// Log login activity
        /// لاگ کردن فعالیت ورود به سیستم
        /// </summary>
        public async Task LogLoginAsync(string adminUserId, string adminUsername, bool isSuccess = true)
        {
            var activityType = isSuccess ? AdminActivityType.Login : AdminActivityType.FailedLogin;
            var description = isSuccess ? "ورود موفق به سیستم" : "تلاش ناموفق برای ورود به سیستم";

            await LogActivityAsync(
                adminUserId,
                adminUsername,
                activityType,
                description,
                isSuccess: isSuccess
            );
        }

        /// <summary>
        /// Log logout activity
        /// لاگ کردن فعالیت خروج از سیستم
        /// </summary>
        public async Task LogLogoutAsync(string adminUserId, string adminUsername)
        {
            await LogActivityAsync(
                adminUserId,
                adminUsername,
                AdminActivityType.Logout,
                "خروج از سیستم"
            );
        }

        /// <summary>
        /// Log data export activity
        /// لاگ کردن فعالیت دریافت داده
        /// </summary>
        public async Task LogDataExportAsync(string adminUserId, string adminUsername, string exportType, int recordCount)
        {
            var details = new
            {
                ExportType = exportType,
                RecordCount = recordCount,
                ExportedAt = DateTime.Now
            };

            await LogActivityAsync(
                adminUserId,
                adminUsername,
                AdminActivityType.DataExport,
                $"دریافت داده: {exportType} ({recordCount} رکورد)",
                JsonSerializer.Serialize(details)
            );
        }

        /// <summary>
        /// Get client IP address from the current request
        /// دریافت آدرس IP کلاینت از درخواست فعلی
        /// </summary>
        private string? GetClientIpAddress()
        {
            if (_httpContextAccessor.HttpContext == null)
                return null;

            var ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString();

            // Handle IPv4-mapped IPv6 addresses
            if (ipAddress != null && ipAddress.Contains("::ffff:"))
            {
                ipAddress = ipAddress.Replace("::ffff:", "");
            }

            return ipAddress;
        }

        /// <summary>
        /// Get admin activities for a specific user
        /// دریافت فعالیت‌های ادمین برای یک کاربر خاص
        /// </summary>
        public async Task<List<AdminActivity>> GetUserActivitiesAsync(string adminUserId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.AdminActivities.Where(a => a.AdminUserId == adminUserId);

            if (fromDate.HasValue)
                query = query.Where(a => a.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.Timestamp <= toDate.Value);

            return await query.OrderByDescending(a => a.Timestamp).ToListAsync();
        }

        /// <summary>
        /// Get all admin activities with optional filtering
        /// دریافت تمام فعالیت‌های ادمین با فیلتر اختیاری
        /// </summary>
        public async Task<List<AdminActivity>> GetAllActivitiesAsync(
            string? adminUserId = null,
            AdminActivityType? activityType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? limit = null)
        {
            var query = _context.AdminActivities.AsQueryable();

            if (!string.IsNullOrEmpty(adminUserId))
                query = query.Where(a => a.AdminUserId == adminUserId);

            if (activityType.HasValue)
                query = query.Where(a => a.ActivityType == activityType.Value);

            if (fromDate.HasValue)
                query = query.Where(a => a.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.Timestamp <= toDate.Value);

            var orderedQuery = query.OrderByDescending(a => a.Timestamp);

            if (limit.HasValue)
                orderedQuery = (IOrderedQueryable<AdminActivity>)orderedQuery.Take(limit.Value);

            return await orderedQuery.ToListAsync();
        }

        /// <summary>
        /// Get activity statistics
        /// دریافت آمار فعالیت‌ها
        /// </summary>
        public async Task<Dictionary<string, int>> GetActivityStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.AdminActivities.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(a => a.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.Timestamp <= toDate.Value);

            var stats = await query
                .GroupBy(a => a.ActivityType)
                .Select(g => new { ActivityType = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ActivityType.ToString(), x => x.Count);

            return stats;
        }

        /// <summary>
        /// Log pool balance change activity
        /// لاگ کردن فعالیت تغییر موجودی صندوق 
        /// </summary>
        public async Task LogPoolBalanceChangeAsync(
            int poolId,
            string currencyCode,
            decimal oldBalance,
            decimal newBalance,
            decimal difference,
            string reason,
            string adminUserId,
            string adminUsername)
        {
            var details = new
            {
                PoolId = poolId,
                CurrencyCode = currencyCode,
                OldBalance = oldBalance,
                NewBalance = newBalance,
                Difference = difference,
                Reason = reason,
                UpdatedAt = DateTime.Now
            };

            await LogActivityAsync(
                adminUserId,
                adminUsername,
                AdminActivityType.PoolBalanceChanged,
                $"موجودی صندوق  {currencyCode} از {oldBalance:N0} به {newBalance:N0} تغییر یافت",
                JsonSerializer.Serialize(details),
                "CurrencyPool",
                poolId,
                oldBalance.ToString("N0"),
                newBalance.ToString("N0")
            );
        }

        /// <summary>
        /// Log pool statistics reset activity
        /// لاگ کردن فعالیت ریست آمار صندوق 
        /// </summary>
        public async Task LogPoolStatsResetAsync(
            int poolId,
            string currencyCode,
            decimal oldTotalBought,
            decimal oldTotalSold,
            string reason,
            string adminUserId,
            string adminUsername)
        {
            var details = new
            {
                PoolId = poolId,
                CurrencyCode = currencyCode,
                OldTotalBought = oldTotalBought,
                OldTotalSold = oldTotalSold,
                Reason = reason,
                ResetAt = DateTime.Now
            };

            await LogActivityAsync(
                adminUserId,
                adminUsername,
                AdminActivityType.PoolStatsReset,
                $"آمار صندوق  {currencyCode} ریست شد (خرید: {oldTotalBought:N0}, فروش: {oldTotalSold:N0})",
                JsonSerializer.Serialize(details),
                "CurrencyPool",
                poolId,
                $"خرید: {oldTotalBought:N0}, فروش: {oldTotalSold:N0}",
                "ریست شده"
            );
        }

        /// <summary>
        /// Log user edit activity
        /// لاگ ویرایش کاربر
        /// </summary>
        public async Task LogUserEditAsync(
            string adminUserId,
            string adminUsername,
            string targetUserId,
            string targetUsername,
            string changes)
        {
            await LogActivityAsync(
                adminUserId,
                adminUsername,
                AdminActivityType.UserUpdated,
                $"اطلاعات کاربر {targetUsername} ویرایش شد",
                changes,
                "ApplicationUser",
                null,
                null,
                changes
            );
        }
    }
}
