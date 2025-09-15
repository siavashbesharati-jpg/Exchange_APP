using System.Globalization;

namespace ForexExchange.Helpers
{
    /// <summary>
    /// Helper class for consistent date formatting throughout the application
    /// Uses standard formats compatible with HTML5 date inputs and international standards
    /// </summary>
    public static class DateTimeHelper
    {
        /// <summary>
        /// Standard date format for display: Year-Month-Day (yyyy-MM-dd) - Compatible with HTML5
        /// </summary>
        public const string DateDisplayFormat = "yyyy-MM-dd";
        
        /// <summary>
        /// Standard date format for HTML date inputs: Year-Month-Day (yyyy-MM-dd)
        /// </summary>
        public const string DateInputFormat = "yyyy-MM-dd";
        
        /// <summary>
        /// Standard datetime format for display: yyyy-MM-dd HH:mm
        /// </summary>
        public const string DateTimeDisplayFormat = "yyyy-MM-dd HH:mm";
        
        /// <summary>
        /// Standard datetime format for full display: yyyy-MM-dd HH:mm:ss
        /// </summary>
        public const string DateTimeFullDisplayFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>
        /// Standard datetime format for HTML5 datetime-local inputs: yyyy-MM-ddTHH:mm
        /// </summary>
        public const string DateTimeLocalFormat = "yyyy-MM-ddTHH:mm";

        /// <summary>
        /// Invariant culture for consistent formatting across all locales
        /// </summary>
        public static readonly CultureInfo StandardCulture = CultureInfo.InvariantCulture;

        /// <summary>
        /// Formats DateTime for display using standard Year-Month-Day format
        /// </summary>
        public static string ToDisplayDate(this DateTime date)
        {
            return date.ToString(DateDisplayFormat, StandardCulture);
        }

        /// <summary>
        /// Formats DateTime for display using standard Year-Month-Day format (nullable version)
        /// </summary>
        public static string ToDisplayDate(this DateTime? date)
        {
            return date?.ToString(DateDisplayFormat, StandardCulture) ?? "";
        }

        /// <summary>
        /// Formats DateTime for HTML5 date input (yyyy-MM-dd format)
        /// </summary>
        public static string ToInputDate(this DateTime date)
        {
            return date.ToString(DateInputFormat, StandardCulture);
        }

        /// <summary>
        /// Formats DateTime for HTML5 date input (yyyy-MM-dd format) (nullable version)
        /// </summary>
        public static string ToInputDate(this DateTime? date)
        {
            return date?.ToString(DateInputFormat, StandardCulture) ?? "";
        }

        /// <summary>
        /// Formats DateTime for display with time using standard format
        /// </summary>
        public static string ToDisplayDateTime(this DateTime date)
        {
            return date.ToString(DateTimeDisplayFormat, StandardCulture);
        }

        /// <summary>
        /// Formats DateTime for display with time using standard format (nullable version)
        /// </summary>
        public static string ToDisplayDateTime(this DateTime? date)
        {
            return date?.ToString(DateTimeDisplayFormat, StandardCulture) ?? "";
        }

        /// <summary>
        /// Formats DateTime for HTML5 datetime-local input (yyyy-MM-ddTHH:mm)
        /// </summary>
        public static string ToDateTimeLocal(this DateTime date)
        {
            return date.ToString(DateTimeLocalFormat, StandardCulture);
        }

        /// <summary>
        /// Formats DateTime for HTML5 datetime-local input (yyyy-MM-ddTHH:mm) (nullable version)
        /// </summary>
        public static string ToDateTimeLocal(this DateTime? date)
        {
            return date?.ToString(DateTimeLocalFormat, StandardCulture) ?? "";
        }

        /// <summary>
        /// Formats DateTime for full display with seconds using standard format
        /// </summary>
        public static string ToFullDisplayDateTime(this DateTime date)
        {
            return date.ToString(DateTimeFullDisplayFormat, StandardCulture);
        }

        /// <summary>
        /// Formats DateTime for full display with seconds using standard format (nullable version)
        /// </summary>
        public static string ToFullDisplayDateTime(this DateTime? date)
        {
            return date?.ToString(DateTimeFullDisplayFormat, StandardCulture) ?? "";
        }

        /// <summary>
        /// Parses date string in standard Year-Month-Day format (yyyy-MM-dd)
        /// </summary>
        public static DateTime ParseDisplayDate(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
                throw new ArgumentException("Date string cannot be empty", nameof(dateString));

            if (DateTime.TryParseExact(dateString, DateDisplayFormat, StandardCulture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            // Try general parsing as fallback
            if (DateTime.TryParse(dateString, StandardCulture, DateTimeStyles.None, out DateTime generalResult))
            {
                return generalResult;
            }

            throw new FormatException($"Unable to parse date string '{dateString}'. Expected format: yyyy-MM-dd");
        }

        /// <summary>
        /// Tries to parse date string in standard Year-Month-Day format
        /// </summary>
        public static bool TryParseDisplayDate(string dateString, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(dateString))
                return false;

            try
            {
                result = ParseDisplayDate(dateString);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Converts HTML5 datetime-local input value (yyyy-MM-ddTHH:mm) to DateTime
        /// </summary>
        public static DateTime ParseDateTimeLocal(string dateTimeString)
        {
            if (string.IsNullOrWhiteSpace(dateTimeString))
                throw new ArgumentException("DateTime string cannot be empty", nameof(dateTimeString));

            if (DateTime.TryParseExact(dateTimeString, DateTimeLocalFormat, StandardCulture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            throw new FormatException($"Unable to parse datetime string '{dateTimeString}'. Expected format: yyyy-MM-ddTHH:mm");
        }

        /// <summary>
        /// Tries to parse HTML5 datetime-local input value (yyyy-MM-ddTHH:mm)
        /// </summary>
        public static bool TryParseDateTimeLocal(string dateTimeString, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(dateTimeString))
                return false;

            return DateTime.TryParseExact(dateTimeString, DateTimeLocalFormat, StandardCulture, DateTimeStyles.None, out result);
        }

        /// <summary>
        /// Gets the current date in display format (yyyy-MM-dd)
        /// </summary>
        public static string CurrentDateDisplay => DateTime.Now.ToDisplayDate();

        /// <summary>
        /// Gets the current date in input format (yyyy-MM-dd)
        /// </summary>
        public static string CurrentDateInput => DateTime.Now.ToInputDate();

        /// <summary>
        /// Gets the current datetime in display format (yyyy-MM-dd HH:mm)
        /// </summary>
        public static string CurrentDateTimeDisplay => DateTime.Now.ToDisplayDateTime();

        /// <summary>
        /// Replacement for ToPersianDateTextify() to maintain backward compatibility
        /// Now uses standard Year-Month-Day format instead of Persian formatting
        /// </summary>
        public static string ToPersianDateTextify(this DateTime date)
        {
            return date.ToDisplayDate();
        }

        /// <summary>
        /// Replacement for ToPersianDateTextify() to maintain backward compatibility (nullable version)
        /// Now uses standard Year-Month-Day format instead of Persian formatting
        /// </summary>
        public static string ToPersianDateTextify(this DateTime? date)
        {
            return date?.ToDisplayDate() ?? "";
        }
    }
}