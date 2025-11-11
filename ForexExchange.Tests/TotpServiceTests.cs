using ForexExchange.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ForexExchange.Tests
{
    public class TotpServiceTests
    {
        private readonly TotpService _sut;

        public TotpServiceTests()
        {
            _sut = new TotpService(new NullLogger<TotpService>());
        }

        [Fact]
        public void GenerateSecret_ShouldReturnBase32String()
        {
            var secret = _sut.GenerateSecret();

            Assert.False(string.IsNullOrWhiteSpace(secret));
            Assert.Matches("^[A-Z2-7]+=*$", secret);
        }

        [Fact]
        public void ValidateCode_ShouldReturnTrueForValidCode()
        {
            var secret = _sut.GenerateSecret();
            var timestamp = DateTime.UtcNow;
            var code = _sut.GenerateCode(secret, timestamp);

            var result = _sut.ValidateCode(secret, code, out var matchedStep);

            Assert.True(result);
            Assert.True(matchedStep >= 0);
        }

        [Fact]
        public void ValidateCode_ShouldReturnFalseForInvalidCode()
        {
            var secret = _sut.GenerateSecret();
            var invalidCode = "000000";

            var result = _sut.ValidateCode(secret, invalidCode, out var matchedStep);

            Assert.False(result);
            Assert.True(matchedStep <= 0);
        }
    }
}

