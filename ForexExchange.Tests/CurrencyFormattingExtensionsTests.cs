using ForexExchange.Extensions;
using Xunit;

namespace ForexExchange.Tests
{
    public class CurrencyFormattingExtensionsTests
    {
        #region FormatCurrency Tests

        [Theory]
        // IRR test cases: Should truncate all decimal places and format with thousand separators
        [InlineData(5457000000.789, "IRR", "5,457,000,000")]
        [InlineData(3046000000.123, "IRR", "3,046,000,000")]
        [InlineData(13000.999, "IRR", "13,000")]
        [InlineData(1000.456, "IRR", "1,000")]
        [InlineData(0.999, "IRR", "0")]
        [InlineData(999.999, "IRR", "999")]
        [InlineData(1234.567, "IRR", "1,234")]
        
        // Non-IRR currencies: Should truncate to 2 decimal places max, remove trailing zeros
        [InlineData(12345.67, "USD", "12,345.67")]
        [InlineData(12345.678, "USD", "12,345.67")]  // Truncated to 2 decimals
        [InlineData(12345.6789, "USD", "12,345.67")] // Truncated to 2 decimals
        [InlineData(12345.60, "USD", "12,345.6")]    // Trailing zero removed
        [InlineData(12345.00, "USD", "12,345")]      // All trailing zeros removed
        [InlineData(12345.10, "USD", "12,345.1")]    // One trailing zero removed
        [InlineData(12345.999, "EUR", "12,345.99")]  // Truncated to 2 decimals
        [InlineData(12345.001, "EUR", "12,345")]     // Truncated and trailing zeros removed
        [InlineData(0.999, "AED", "0.99")]           // Truncated to 2 decimals
        [InlineData(0.001, "AED", "0")]              // Truncated and trailing zeros removed
        [InlineData(123.456, "CNY", "123.45")]       // Truncated to 2 decimals
        [InlineData(123.00, "CNY", "123")]           // Trailing zeros removed
        public void FormatCurrency_ShouldFormatCorrectly(decimal value, string currencyCode, string expected)
        {
            // Act
            var result = value.FormatCurrency(currencyCode);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        // Test integer formatting
        [InlineData(1000, "1,000")]
        [InlineData(1234567, "1,234,567")]
        [InlineData(0, "0")]
        [InlineData(999, "999")]
        public void FormatCurrency_Integer_ShouldFormatCorrectly(int value, string expected)
        {
            // Act
            var result = value.FormatCurrency();

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        // Test long formatting
        [InlineData(1000L, "1,000")]
        [InlineData(9876543210L, "9,876,543,210")]
        [InlineData(0L, "0")]
        public void FormatCurrency_Long_ShouldFormatCorrectly(long value, string expected)
        {
            // Act
            var result = value.FormatCurrency();

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void FormatCurrency_NullableDecimal_ShouldFormatCorrectly()
        {
            // Test null value
            decimal? nullValue = null;
            Assert.Equal("", nullValue.FormatCurrency());
            Assert.Equal("", nullValue.FormatCurrency("USD"));
            
            // Test with actual values
            decimal? usdValue = 1234.56m;
            Assert.Equal("1,234.56", usdValue.FormatCurrency("USD"));
            
            decimal? irrValue = 1234.567m;
            Assert.Equal("1,234", irrValue.FormatCurrency("IRR"));
        }

        [Fact]
        public void FormatCurrency_NullableDouble_ShouldFormatCorrectly()
        {
            // Test null value
            double? nullValue = null;
            Assert.Equal("", nullValue.FormatCurrency());
            Assert.Equal("", nullValue.FormatCurrency("USD"));
            
            // Test with actual values
            double? usdValue = 1234.56;
            Assert.Equal("1,234.56", usdValue.FormatCurrency("USD"));
            
            double? irrValue = 1234.567;
            Assert.Equal("1,234", irrValue.FormatCurrency("IRR"));
        }

        [Theory]
        // Test double and float conversion
        [InlineData(1234.567, "USD", "1,234.56")]
        [InlineData(1234.567, "IRR", "1,234")]
        public void FormatCurrency_Double_ShouldFormatCorrectly(double value, string currencyCode, string expected)
        {
            // Act
            var result = value.FormatCurrency(currencyCode);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        // Test float conversion
        [InlineData(1234.567f, "USD", "1,234.56")]
        [InlineData(1234.567f, "IRR", "1,234")]
        public void FormatCurrency_Float_ShouldFormatCorrectly(float value, string currencyCode, string expected)
        {
            // Act
            var result = value.FormatCurrency(currencyCode);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region TruncateToCurrencyDefaults Tests

        [Theory]
        // IRR test cases: Should truncate all decimal places (no rounding)
        [InlineData(1234.999, "IRR", 1234)]
        [InlineData(1234.001, "IRR", 1234)]
        [InlineData(1234.567, "IRR", 1234)]
        [InlineData(0.999, "IRR", 0)]
        [InlineData(999.999, "IRR", 999)]
        [InlineData(1000.456, "IRR", 1000)]
        [InlineData(1000.000, "IRR", 1000)]
        [InlineData(-1234.567, "IRR", -1234)]
        [InlineData(-0.999, "IRR", 0)]
        
        // Non-IRR currencies: Should truncate to exactly 2 decimal places (no rounding)
        [InlineData(12345.6789, "USD", 12345.67)]
        [InlineData(12345.999, "USD", 12345.99)]
        [InlineData(12345.001, "USD", 12345.00)]
        [InlineData(123.456789, "EUR", 123.45)]
        [InlineData(123.999, "EUR", 123.99)]
        [InlineData(123.001, "EUR", 123.00)]
        [InlineData(0.999, "AED", 0.99)]
        [InlineData(0.001, "AED", 0.00)]
        [InlineData(100.005, "CNY", 100.00)]
        [InlineData(100.995, "CNY", 100.99)]
        [InlineData(-123.456, "USD", -123.45)]
        [InlineData(-123.999, "USD", -123.99)]
        public void TruncateToCurrencyDefaults_ShouldTruncateCorrectly(decimal value, string currencyCode, decimal expected)
        {
            // Act
            var result = value.TruncateToCurrencyDefaults(currencyCode);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TruncateToCurrencyDefaults_IRR_ShouldTruncateAllDecimals()
        {
            // Test that IRR always truncates all decimal places, never rounds
            Assert.Equal(0m, 0.999m.TruncateToCurrencyDefaults("IRR"));
            Assert.Equal(1m, 1.999m.TruncateToCurrencyDefaults("IRR"));
            Assert.Equal(999m, 999.999m.TruncateToCurrencyDefaults("IRR"));
            Assert.Equal(1000m, 1000.999m.TruncateToCurrencyDefaults("IRR"));
            Assert.Equal(1234m, 1234.567m.TruncateToCurrencyDefaults("IRR"));
            
            // Test negative values
            Assert.Equal(-1m, (-1.999m).TruncateToCurrencyDefaults("IRR"));
            Assert.Equal(-999m, (-999.999m).TruncateToCurrencyDefaults("IRR"));
        }

        [Fact]
        public void TruncateToCurrencyDefaults_NonIRR_ShouldTruncateToTwoDecimals()
        {
            // Test that non-IRR currencies truncate to exactly 2 decimal places, never round
            Assert.Equal(12.34m, 12.349m.TruncateToCurrencyDefaults("USD"));
            Assert.Equal(12.99m, 12.999m.TruncateToCurrencyDefaults("USD"));
            Assert.Equal(0.99m, 0.999m.TruncateToCurrencyDefaults("EUR"));
            Assert.Equal(0.00m, 0.009m.TruncateToCurrencyDefaults("EUR"));
            Assert.Equal(123.45m, 123.456789m.TruncateToCurrencyDefaults("AED"));
            
            // Test negative values
            Assert.Equal(-12.34m, (-12.349m).TruncateToCurrencyDefaults("USD"));
            Assert.Equal(-12.99m, (-12.999m).TruncateToCurrencyDefaults("USD"));
        }

        [Theory]
        // Test null currency code (should behave like non-IRR)
        [InlineData(12345.6789, null, 12345.67)]
        [InlineData(123.999, null, 123.99)]
        [InlineData(0.999, null, 0.99)]
        public void TruncateToCurrencyDefaults_NullCurrency_ShouldTruncateToTwoDecimals(decimal value, string? currencyCode, decimal expected)
        {
            // Act
            var result = value.TruncateToCurrencyDefaults(currencyCode);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion
    }
}
