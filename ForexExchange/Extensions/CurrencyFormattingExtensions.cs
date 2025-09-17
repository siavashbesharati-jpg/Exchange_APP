using System;
using System.Globalization;

namespace ForexExchange.Extensions
{
    /// <summary>
    /// Extension methods for formatting currency values with thousand separators
    /// </summary>
    public static class CurrencyFormattingExtensions
    {
        /// <summary>
        /// Format decimal value with thousand separators based on currency code
        /// </summary>
        /// <param name="value">The decimal value to format</param>
        /// <param name="currencyCode">Currency code (IRR, USD, EUR, etc.)</param>
        /// <returns>Formatted string with thousand separators</returns>
        public static string FormatCurrency(this decimal value, string? currencyCode = null)
        {
            // For IRR (Iranian Rial), use smart rounding to nearest appropriate unit
            if (currencyCode == "IRR")
            {
                return RoundIRRToNearestUnit(value).ToString("N0", CultureInfo.InvariantCulture);
            }

            // For non-IRR currencies, use up to 3 decimal places with proper rounding, remove trailing zeros
            var rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);

            // Format with 3 decimals first, then remove trailing zeros
            var formatted = rounded.ToString("N3", CultureInfo.InvariantCulture);

            // Remove trailing zeros and decimal point if no decimals remain
            if (formatted.Contains("."))
            {
                formatted = formatted.TrimEnd('0').TrimEnd('.');
            }

            return formatted;
        }

        /// <summary>
        /// Format double value with thousand separators based on currency code
        /// </summary>
        /// <param name="value">The double value to format</param>
        /// <param name="currencyCode">Currency code (IRR, USD, EUR, etc.)</param>
        /// <returns>Formatted string with thousand separators</returns>
        public static string FormatCurrency(this double value, string? currencyCode = null)
        {
            return ((decimal)value).FormatCurrency(currencyCode);
        }

        /// <summary>
        /// Format float value with thousand separators based on currency code
        /// </summary>
        /// <param name="value">The float value to format</param>
        /// <param name="currencyCode">Currency code (IRR, USD, EUR, etc.)</param>
        /// <returns>Formatted string with thousand separators</returns>
        public static string FormatCurrency(this float value, string? currencyCode = null)
        {
            return ((decimal)value).FormatCurrency(currencyCode);
        }

        /// <summary>
        /// Format integer value with thousand separators
        /// </summary>
        /// <param name="value">The integer value to format</param>
        /// <returns>Formatted string with thousand separators</returns>
        public static string FormatCurrency(this int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format long value with thousand separators
        /// </summary>
        /// <param name="value">The long value to format</param>
        /// <returns>Formatted string with thousand separators</returns>
        public static string FormatCurrency(this long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format nullable decimal value with thousand separators
        /// </summary>
        /// <param name="value">The nullable decimal value to format</param>
        /// <param name="currencyCode">Currency code (IRR, USD, EUR, etc.)</param>
        /// <returns>Formatted string with thousand separators, or empty string if null</returns>
        public static string FormatCurrency(this decimal? value, string? currencyCode = null)
        {
            return value?.FormatCurrency(currencyCode) ?? "";
        }

        /// <summary>
        /// Format nullable double value with thousand separators
        /// </summary>
        /// <param name="value">The nullable double value to format</param>
        /// <param name="currencyCode">Currency code (IRR, USD, EUR, etc.)</param>
        /// <returns>Formatted string with thousand separators, or empty string if null</returns>
        public static string FormatCurrency(this double? value, string? currencyCode = null)
        {
            return value?.FormatCurrency(currencyCode) ?? "";
        }

        /// <summary>
        /// رندینگ هوشمند برای مقادیر ریال ایران (IRR).
        /// این متد به صورت خودکار بر اساس بزرگی عدد، واحد رند کردن را انتخاب کرده
        /// و مقدار را همیشه به سمت بالا (Ceiling) رند می‌کند.
        /// </summary>
        /// <param name="value">
        /// مقدار ریالی که باید رند شود. 
        /// این عدد می‌تواند مثبت یا منفی باشد.
        /// </param>
        /// <returns>
        /// مقدار رند شده به نزدیک‌ترین واحد مناسب.
        /// 
        /// جدول رندینگ:
        /// <list type="bullet">
        /// <item>
        /// بالاتر یا مساوی 1,000,000,000 → رند به میلیون
        /// </item>
        /// <item>
        /// از 1,000,000 تا کمتر از 1,000,000,000 → رند به میلیون
        /// </item>
        /// <item>
        /// از 100,000 تا کمتر از 1,000,000 → رند به ده‌هزار
        /// </item>
        /// <item>
        /// از 1,000 تا کمتر از 100,000 → رند به هزار
        /// </item>
        /// <item>
        /// از 100 تا کمتر از 1,000 → رند به صد
        /// </item>
        /// <item>
        /// از 10 تا کمتر از 100 → رند به ده
        /// </item>
        /// کمتر از 10 → رند به یک
        /// </list>
        /// 
        /// مثال‌ها:
        /// <code>
        /// RoundIRRToNearestUnit(1_445_990_000) → 1_446_000_000
        /// RoundIRRToNearestUnit(1_445_949_954) → 1_445_950_000
        /// RoundIRRToNearestUnit(123_456) → 130_000
        /// RoundIRRToNearestUnit(457) → 500
        /// RoundIRRToNearestUnit(89) → 90
        /// RoundIRRToNearestUnit(3_299_999_957) → 3_300_000_000
        /// </code>
        /// </returns>
        /// <remarks>
        /// برای اعداد منفی نیز این متد به سمت صفر (کمتر منفی) رند می‌کند.
        /// </remarks>
        private static decimal RoundIRRToNearestUnit(decimal value)
        {
            // صفر
            if (value == 0) return 0;

            bool isNegative = value < 0;
            var absValue = Math.Abs(value);

            // انتخاب roundTo بر اساس بزرگی عدد
            decimal roundTo;
            if (absValue >= 1_000_000_000m)
                roundTo = 1_000_000m;         // بالای یک میلیارد → به میلیون
            else if (absValue >= 1_000_000m)
                roundTo = 1_000_000m;         // از یک میلیون تا یک میلیارد → به میلیون
            else if (absValue >= 100_000m)
                roundTo = 10_000m;            // از صد هزار تا یک میلیون → به ده هزار
            else if (absValue >= 1_000m)
                roundTo = 1_000m;             // از هزار تا صد هزار → به هزار
            else if (absValue >= 100m)
                roundTo = 100m;               // از صد تا هزار → به صد
            else if (absValue >= 10m)
                roundTo = 10m;                // از ده تا صد → به ده
            else
                roundTo = 1m;                 // کمتر از ده → به یک

            // انجام رندینگ رو به بالا
            var roundedAbs = Math.Ceiling(absValue / roundTo) * roundTo;

            // بازگرداندن با علامت صحیح
            return isNegative ? -roundedAbs : roundedAbs;
        }
    }
}
