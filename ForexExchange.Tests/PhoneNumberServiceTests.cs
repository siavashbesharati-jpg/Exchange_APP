using ForexExchange.Services;
using Xunit;

namespace ForexExchange.Tests
{
    public class PhoneNumberServiceTests
    {
        [Theory]
        [InlineData("09123456789", "00989123456789")]
        [InlineData("+989121234567", "00989121234567")]
        [InlineData("00989121234567", "00989121234567")]
        [InlineData("9121234567", "00989121234567")]
        [InlineData("+911234567890", "00911234567890")]
        [InlineData("0012345678901", "0012345678901")]
        [InlineData("", "")]
        [InlineData("abc", "0098")]
        [InlineData("+", "00")]
        public void NormalizePhoneNumber_ShouldNormalizeCorrectly(string input, string expected)
        {
            // Act
            var result = PhoneNumberService.NormalizePhoneNumber(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("00989123456789", true)]
        [InlineData("00911234567890", true)]
        [InlineData("0012345678901", true)]
        [InlineData("0098", true)] // Technically valid based on current logic
        [InlineData("123", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidNormalizedPhoneNumber_ShouldValidateCorrectly(string normalizedPhoneNumber, bool expected)
        {
            // Act
            var result = PhoneNumberService.IsValidNormalizedPhoneNumber(normalizedPhoneNumber);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("00989123456789", "+98 912 3456789")]
        [InlineData("00919876543210", "+91 9876543210")]
        [InlineData("0012025550125", "+1 2025550125")]
        [InlineData("00442079460000", "+44 2079460000")]
        [InlineData("0098", "+98 ")]
        [InlineData("invalid", "invalid")]
        public void GetDisplayFormat_ShouldFormatCorrectly(string normalizedPhoneNumber, string expected)
        {
            // Act
            var result = PhoneNumberService.GetDisplayFormat(normalizedPhoneNumber);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("09123456789", true)]
        [InlineData("+989121234567", true)]
        [InlineData("00989121234567", true)]
        [InlineData("+911234567890", false)]
        [InlineData("invalid", false)]
        public void IsValidIranianPhoneNumber_ShouldValidateCorrectly(string input, bool expected)
        {
            // Act
            var result = PhoneNumberService.IsValidIranianPhoneNumber(input);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
