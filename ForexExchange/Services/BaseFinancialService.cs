using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Extensions;

namespace ForexExchange.Services
{
    /// <summary>
    /// Base class providing common functionality for financial services
    /// کلاس پایه برای ارائه قابلیت‌های مشترک سرویس‌های مالی
    /// </summary>
    public abstract class BaseFinancialService
    {
        protected readonly ForexDbContext _context;

        protected BaseFinancialService(ForexDbContext context)
        {
            _context = context;
        }

        #region Common Helper Methods

        /// <summary>
        /// Formats DateTime to Gregorian date string
        /// تبدیل تاریخ به رشته تاریخ میلادی
        /// </summary>
        protected string FormatGregorianDate(DateTime date)
        {
            return date.ToString("yyyy/MM/dd");
        }

        /// <summary>
        /// Formats DateTime to time string
        /// تبدیل زمان به رشته زمان
        /// </summary>
        protected string FormatTime(DateTime dateTime)
        {
            return dateTime.ToString("HH:mm:ss");
        }

        /// <summary>
        /// Validates and normalizes date range parameters
        /// اعتبارسنجی و عادی‌سازی پارامترهای بازه تاریخ
        /// </summary>
        protected (DateTime fromDate, DateTime toDate) ValidateDateRange(DateTime? fromDate, DateTime? toDate)
        {
            var actualFromDate = fromDate ?? DateTime.MinValue;
            var actualToDate = toDate ?? DateTime.MaxValue;

            // Ensure toDate includes the entire day
            if (toDate.HasValue && toDate.Value.TimeOfDay == TimeSpan.Zero)
            {
                actualToDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
            }

            return (actualFromDate, actualToDate);
        }

        /// <summary>
        /// Formats currency amount with proper formatting
        /// قالب‌بندی مبلغ ارزی با فرمت مناسب
        /// </summary>
        protected string FormatCurrencyAmount(decimal amount)
        {
            // Use unified formatting: IRR truncates decimals, others show 2 decimals
            return amount.FormatCurrency("IRR");
        }

        /// <summary>
        /// Calculates common transaction statistics
        /// محاسبه آمار مشترک تراکنش‌ها
        /// </summary>
        protected (int totalTransactions, int todayTransactions) CalculateTransactionStatistics<T>(
            IEnumerable<T> transactions, Func<T, DateTime> dateSelector)
        {
            var today = DateTime.Today;
            var totalTransactions = transactions.Count();
            var todayTransactions = transactions.Count(item => dateSelector(item).Date == today);
            
            return (totalTransactions, todayTransactions);
        }

        #endregion
    }
}