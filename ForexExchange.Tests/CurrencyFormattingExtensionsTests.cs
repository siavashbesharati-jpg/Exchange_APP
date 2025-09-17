using Xunit;
using ForexExchange.Extensions;

namespace ForexExchange.Tests
{
    /// <summary>
    /// Unit tests for IRR currency formatting with smart rounding behavior.
    /// Tests the smart rounding logic that automatically detects the best unit
    /// and always rounds UP (ceiling) to the nearest multiple (million, thousand, hundred…)
    /// </summary>
    public class CurrencyFormattingExtensionsTests
    {
        #region Billion+ Range Tests (≥ 1,000,000,000)

        [Theory]
        [InlineData(1_445_990_000, "1,446,000,000")]
        [InlineData(1_000_000_000, "1,000,000,000")]
        [InlineData(1_000_000_001, "1,001,000,000")]
        [InlineData(1_500_000_000, "1,500,000,000")]
        [InlineData(2_750_000_000, "2,750,000,000")]
        [InlineData(9_999_999_999, "10,000,000,000")]
        public void FormatCurrency_BillionRange_ShouldRoundUpToNearestMillion(decimal input, string expected)
        {
            var result = input.FormatCurrency("IRR");
            Assert.Equal(expected, result);
        }

        #endregion

        #region Million Range Tests (1,000,000 - 999,999,999)

        [Theory]
        [InlineData(1_445_949_954, "1,446,000,000")]
        [InlineData(1_000_000, "1,000,000")]
        [InlineData(1_000_001, "2,000,000")]
        [InlineData(1_500_000, "2,000,000")]
        [InlineData(2_750_000, "3,000,000")]
        [InlineData(999_999_999, "1,000,000,000")]
        [InlineData(500_000_000, "500,000,000")]
        public void FormatCurrency_MillionRange_ShouldRoundUpToNearestAppropriate(decimal input, string expected)
        {
            var result = input.FormatCurrency("IRR");
            Assert.Equal(expected, result);
        }

        #endregion

        #region Thousand Range Tests (1,000 - 999,999)

        [Theory]
        [InlineData(123_456, "130,000")]
        [InlineData(1_000, "1,000")]
        [InlineData(1_001, "2,000")]
        [InlineData(1_500, "2,000")]
        [InlineData(2_750, "3,000")]
        [InlineData(999_999, "1,000,000")]
        [InlineData(500_000, "500,000")]
        public void FormatCurrency_ThousandRange_ShouldRoundUpToNearestThousand(decimal input, string expected)
        {
            var result = input.FormatCurrency("IRR");
            Assert.Equal(expected, result);
        }

        #endregion

        #region Hundred Range Tests (100 - 999)

        [Theory]
        [InlineData(457, "500")]
        [InlineData(100, "100")]
        [InlineData(101, "200")]
        [InlineData(150, "200")]
        [InlineData(275, "300")]
        [InlineData(999, "1,000")]
        [InlineData(550, "600")]
        public void FormatCurrency_HundredRange_ShouldRoundUpToNearestHundred(decimal input, string expected)
        {
            var result = input.FormatCurrency("IRR");
            Assert.Equal(expected, result);
        }

        #endregion

        #region Ten Range Tests (10 - 99)

        [Theory]
        [InlineData(89, "90")]
        [InlineData(10, "10")]
        [InlineData(11, "20")]
        [InlineData(15, "20")]
        [InlineData(27, "30")]
        [InlineData(99, "100")]
        [InlineData(55, "60")]
        public void FormatCurrency_TenRange_ShouldRoundUpToNearestTen(decimal input, string expected)
        {
            var result = input.FormatCurrency("IRR");
            Assert.Equal(expected, result);
        }

        #endregion

        #region Single Digit Tests (0 - 9)

        [Theory]
        [InlineData(0, "0")]
        [InlineData(1, "1")]
        [InlineData(1.1, "2")]
        [InlineData(5.7, "6")]
        [InlineData(3.2, "4")]
        [InlineData(7.5, "8")]
        [InlineData(9.9, "10")]
        public void FormatCurrency_SingleDigitRange_ShouldRoundUpToNearestWhole(decimal input, string expected)
        {
            var result = input.FormatCurrency("IRR");
            Assert.Equal(expected, result);
        }

        #endregion

        #region Negative Number Tests

        [Theory]
        [InlineData(-1_445_990_000, "-1,446,000,000")]
        [InlineData(-1_500_000, "-2,000,000")]
        [InlineData(-457, "-500")]
        [InlineData(-89, "-90")]
        [InlineData(-15, "-20")]
        [InlineData(-7.5, "-8")]
        [InlineData(-1.1, "-2")]
        public void FormatCurrency_NegativeNumbers_ShouldRoundCeilingTowardsZero(decimal input, string expected)
        {
            var result = input.FormatCurrency("IRR");
            Assert.Equal(expected, result);
        }

        #endregion

        #region Edge Cases

        [Theory]
        [InlineData(999_999_999_999, "1,000,000,000,000")]
        [InlineData(0.1, "1")]
        [InlineData(-0.1, "-1")]
        [InlineData(0.9999, "1")]
        [InlineData(-0.9999, "-1")]
        public void FormatCurrency_EdgeCases_ShouldHandleCorrectly(decimal input, string expected)
        {
            var result = input.FormatCurrency("IRR");
            Assert.Equal(expected, result);
        }

        #endregion

        #region Non-IRR Currencies

        [Theory]
        [InlineData(1234.567, "USD", "1,234.567")]
        [InlineData(1234.5678, "USD", "1,234.568")]
        [InlineData(1000.000, "EUR", "1,000")]
        [InlineData(123.45, "GBP", "123.45")]
        public void FormatCurrency_NonIRRCurrencies_ShouldUseOriginalLogic(decimal input, string currency, string expected)
        {
            var result = input.FormatCurrency(currency);
            Assert.Equal(expected, result);
        }

        #endregion

        #region Null and Empty Currency Code Tests

        [Fact]
        public void FormatCurrency_NullCurrencyCode_ShouldUseNonIRRLogic()
        {
            decimal input = 1234.567m;
            var result = input.FormatCurrency(null);
            Assert.Equal("1,234.567", result);
        }

        [Fact]
        public void FormatCurrency_EmptyCurrencyCode_ShouldUseNonIRRLogic()
        {
            decimal input = 1234.567m;
            var result = input.FormatCurrency("");
            Assert.Equal("1,234.567", result);
        }

        #endregion

        #region Integration User Examples

        [Fact]
        public void FormatCurrency_UserProvidedExamples_ShouldMatchExpected()
        {
            var examples = new[]
            {
                new { Input = 1_445_990_000m, Expected = "1,446,000,000" },
                new { Input = 1_445_949_954m, Expected = "1,446,000,000" },
                new { Input = 123_456m, Expected = "130,000" },
                new { Input = 457m, Expected = "500" },
                new { Input = 89m, Expected = "90" }
            };

            foreach (var example in examples)
            {
                var result = example.Input.FormatCurrency("IRR");
                Assert.Equal(example.Expected, result);
            }
        }

        #endregion

        #region Performance Large Numbers

        [Theory]
        [InlineData(999_999_999_999_999, "1,000,000,000,000,000")]
        [InlineData(1_000_000_000_000_000, "1,000,000,000,000,000")]
        [InlineData(1_000_000_000_000_001, "1,000,000,001,000,000")]
        public void FormatCurrency_VeryLargeNumbers_ShouldHandleCorrectly(decimal input, string expected)
        {
            var result = input.FormatCurrency("IRR");
            Assert.Equal(expected, result);
        }

        #endregion
    }
}
