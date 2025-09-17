using ForexExchange.Extensions;
using Xunit;

namespace ForexExchange.Tests
{
    public class CurrencyFormattingExtensionsTests
    {
        [Theory]
        [InlineData(5456942398, "IRR", "5,456,943")]
        [InlineData(5456942398.57, "IRR", "5,456,943")]
        [InlineData(3045691564, "IRR", "3,045,692")]
        [InlineData(12345, "IRR", "13")]
        [InlineData(1000, "IRR", "1")]
        [InlineData(999, "IRR", "1")]
        [InlineData(0, "IRR", "0")]
        [InlineData(12345.67, "USD", "12,345.67")]
        [InlineData(12345.678, "EUR", "12,345.678")]
        [InlineData(12345.6, "AED", "12,345.6")]
        [InlineData(12345.0, "CNY", "12,345")]
        [InlineData(12345.0, "TRY", "12,345")]
        [InlineData(12345.0, "OMR", "12,345")]
        [InlineData(12345.0, "JPY", "12,345")]
        public void FormatCurrency_ShouldFormatCorrectly(decimal value, string currencyCode, string expected)
        {
            // Act
            var result = value.FormatCurrency(currencyCode);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(3045691.564, "IRR", 3046000)]
        [InlineData(12345, "IRR", 13000)]
        [InlineData(1000, "IRR", 1000)]
        [InlineData(999, "IRR", 1000)]
        [InlineData(0, "IRR", 0)]
        [InlineData(25591000, "IRR", 25591000)]

        [InlineData(12345.6789, "USD", 12345.679)]
        [InlineData(123.456, "EUR", 123.456)]
        [InlineData(123.45, "GBP", 123.450)]
        public void RoundToCurrencyDefaults_ShouldRoundCorrectly(decimal value, string currencyCode, decimal expected)
        {
            // Act
            var result = value.RoundToCurrencyDefaults(currencyCode);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
