using ForexExchange.Extensions;
using Xunit;

namespace ForexExchange.Tests
{
    public class CurrencyFormattingExtensionsTests
    {
        [Theory]
        // Test cases for IRR: Input values are pre-rounded (as if from DB)
        [InlineData(5457000000, "IRR", "5,457,000,000")]
        [InlineData(3046000000, "IRR", "3,046,000,000")]
        [InlineData(13000, "IRR", "13,000")]
        [InlineData(1000, "IRR", "1,000")]
        [InlineData(0, "IRR", "0")]
        
        // Test cases for other currencies
        [InlineData(12345.67, "USD", "12,345.67")]
        [InlineData(12345.678, "EUR", "12,345.678")]
        [InlineData(12345.6, "AED", "12,345.6")]
        [InlineData(12345.0, "CNY", "12,345")]
        [InlineData(12345.12345678, "TRY", "12,345.12345678")]
        [InlineData(12345.00000000, "OMR", "12,345")]
        [InlineData(12345.100, "JPY", "12,345.1")]
        public void FormatCurrency_ShouldFormatCorrectly(decimal value, string currencyCode, string expected)
        {
            // Act
            var result = value.FormatCurrency(currencyCode);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        // IRR rounding to nearest 1000 test cases
        [InlineData(1, "IRR", 0)]          // 1 → 0
        [InlineData(499, "IRR", 0)]        // 499 → 0
        [InlineData(500, "IRR", 1000)]     // 500 → 1000
        [InlineData(999, "IRR", 1000)]     // 999 → 1000
        [InlineData(18570000, "IRR", 18570000)]     // 18570000 → 18570000
        [InlineData(18571000, "IRR", 18571000)]     // 18571000 → 18571000
        [InlineData(18571234, "IRR", 18571000)]     // 18571234 → 18571000
        [InlineData(18571789, "IRR", 18572000)]     // 18571789 → 18572000
        [InlineData(580456, "IRR", 580000)]         // 580456 → 580000
        [InlineData(580789, "IRR", 581000)]         // 580789 → 581000
        
        // Additional IRR edge cases
        [InlineData(0, "IRR", 0)]          // 0 → 0
        [InlineData(1500, "IRR", 2000)]    // 1500 → 2000
        [InlineData(2500, "IRR", 3000)]    // 2500 → 3000 (rounds up at exactly .5)
        [InlineData(3500, "IRR", 4000)]    // 3500 → 4000
        [InlineData(25591499, "IRR", 25591000)]  // Just under .5
        [InlineData(25591500, "IRR", 25592000)]  // Exactly .5, rounds up
        [InlineData(25591501, "IRR", 25592000)]  // Just over .5

        // Non-IRR currency test cases (3 decimal places)
        [InlineData(12345.6789, "USD", 12345.679)]
        [InlineData(123.456, "EUR", 123.456)]
        [InlineData(123.45, "GBP", 123.450)]
        [InlineData(123.4567890, "AED", 123.457)]
        [InlineData(100.0001, "CNY", 100.000)]
        public void RoundToCurrencyDefaults_ShouldRoundCorrectly(decimal value, string currencyCode, decimal expected)
        {
            // Act
            var result = value.RoundToCurrencyDefaults(currencyCode);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void RoundToCurrencyDefaults_IRR_ShouldRoundToNearestThousand()
        {
            // Test the specific cases mentioned in requirements
            
            // Values that should round down to 0
            Assert.Equal(0m, 1m.RoundToCurrencyDefaults("IRR"));
            Assert.Equal(0m, 499m.RoundToCurrencyDefaults("IRR"));
            
            // Values that should round up to 1000
            Assert.Equal(1000m, 500m.RoundToCurrencyDefaults("IRR"));
            Assert.Equal(1000m, 999m.RoundToCurrencyDefaults("IRR"));
            
            // Values that should remain unchanged (already at thousand boundary)
            Assert.Equal(18570000m, 18570000m.RoundToCurrencyDefaults("IRR"));
            Assert.Equal(18571000m, 18571000m.RoundToCurrencyDefaults("IRR"));
            
            // Values in the middle of thousands
            Assert.Equal(18571000m, 18571234m.RoundToCurrencyDefaults("IRR"));
            Assert.Equal(18572000m, 18571789m.RoundToCurrencyDefaults("IRR"));
            Assert.Equal(580000m, 580456m.RoundToCurrencyDefaults("IRR"));
            Assert.Equal(581000m, 580789m.RoundToCurrencyDefaults("IRR"));
            
            // Test exactly at .5 (should round up due to AwayFromZero)
            Assert.Equal(2000m, 1500m.RoundToCurrencyDefaults("IRR"));
            Assert.Equal(3000m, 2500m.RoundToCurrencyDefaults("IRR"));
        }
    }
}
