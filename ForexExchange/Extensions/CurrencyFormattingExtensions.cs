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
            // For IRR, display the value as-is with thousand separators (no division)
            if (currencyCode == "IRR")
            {
                return value.ToString("N0", CultureInfo.InvariantCulture);
            }
            
            // For non-IRR currencies, format with up to 8 decimal places and remove trailing zeros.
            var formatted = value.ToString("N8", CultureInfo.InvariantCulture);
            
            if (formatted.Contains('.'))
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
        /// Rounds a decimal value based on currency-specific rules.
        /// For IRR, rounds to the nearest 1000. For others, rounds to 3 decimal places.
        /// This affects the actual value, not just the display format.
        /// </summary>
        /// <param name="value">The decimal value to round.</param>
        /// <param name="currencyCode">The currency code (e.g., "IRR").</param>
        /// <returns>The rounded decimal value.</returns>
        public static decimal RoundToCurrencyDefaults(this decimal value, string? currencyCode)
        {
            if (currencyCode == "IRR")
            {
                // For IRR, round to the nearest 1000 using banker's rounding
                return Math.Round(value / 1000, 0, MidpointRounding.AwayFromZero) * 1000;
            }
            else
            {
                // For other currencies, round to 3 decimal places.
                return Math.Round(value, 3, MidpointRounding.AwayFromZero);
            }
        }
    }
}
