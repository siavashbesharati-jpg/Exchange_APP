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
        /// GLOBAL FORMATTING RULE: IRR = no decimals (truncate), non-IRR = 2 decimals (truncate)
        /// </summary>
        /// <param name="value">The decimal value to format</param>
        /// <param name="currencyCode">Currency code (IRR, USD, EUR, etc.)</param>
        /// <returns>Formatted string with thousand separators</returns>
        public static string FormatCurrency(this decimal value, string? currencyCode = null)
        {
            // For IRR, truncate all decimal places and display with thousand separators
            if (currencyCode == "IRR")
            {
                var truncatedValue = Math.Truncate(value);
                return truncatedValue.ToString("N0", CultureInfo.InvariantCulture);
            }
            
            // For non-IRR currencies, truncate to exactly 2 decimal places (no rounding)
            var truncatedToTwoDecimals = Math.Truncate(value * 100) / 100;
            return truncatedToTwoDecimals.ToString("N2", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format double value with thousand separators based on currency code
        /// GLOBAL FORMATTING RULE: IRR = no decimals (truncate), non-IRR = 2 decimals (truncate)
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
        /// GLOBAL FORMATTING RULE: IRR = no decimals (truncate), non-IRR = 2 decimals (truncate)
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
        /// Truncate a decimal value based on currency-specific rules (NO ROUNDING).
        /// For IRR, truncates all decimal places. For others, truncates to 2 decimal places.
        /// This affects the actual value, not just the display format.
        /// </summary>
        /// <param name="value">The decimal value to truncate.</param>
        /// <param name="currencyCode">The currency code (e.g., "IRR").</param>
        /// <returns>The truncated decimal value.</returns>
        public static decimal TruncateToCurrencyDefaults(this decimal value, string? currencyCode)
        {
            if (currencyCode == "IRR")
            {
                // For IRR, truncate all decimal places (no rounding)
                return Math.Truncate(value);
            }
            else
            {
                // For other currencies, truncate to exactly 2 decimal places (no rounding)
                return Math.Truncate(value * 100) / 100;
            }
        }
    }
}
