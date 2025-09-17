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


        public void FormatCurrency_ShouldFormatCorrectly(decimal value, string currencyCode, string expected)
        {
            // Act
            var result = value.FormatCurrency(currencyCode);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
