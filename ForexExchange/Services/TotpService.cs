using Microsoft.Extensions.Logging;
using OtpNet;

namespace ForexExchange.Services
{
    public interface ITotpService
    {
        string GenerateSecret(int size = 20);
        string GenerateCode(string base32Secret, DateTime? timestamp = null);
        bool ValidateCode(string base32Secret, string code, out long matchedStep);
    }

    public class TotpService : ITotpService
    {
        private readonly ILogger<TotpService> _logger;
        private readonly TimeSpan _timeStep = TimeSpan.FromSeconds(30);
        private readonly int _allowedDriftSteps = 1; // +/-30s

        public TotpService(ILogger<TotpService> logger)
        {
            _logger = logger;
        }

        public string GenerateSecret(int size = 20)
        {
            var bytes = KeyGeneration.GenerateRandomKey(size);
            return Base32Encoding.ToString(bytes);
        }

        public string GenerateCode(string base32Secret, DateTime? timestamp = null)
        {
            var totp = CreateTotp(base32Secret);
            return totp.ComputeTotp(timestamp ?? DateTime.UtcNow);
        }

        public bool ValidateCode(string base32Secret, string code, out long matchedStep)
        {
            matchedStep = -1;

            if (string.IsNullOrWhiteSpace(base32Secret))
            {
                _logger.LogWarning("Attempted to validate OTP with empty secret.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                _logger.LogWarning("Attempted to validate OTP with empty code.");
                return false;
            }

            try
            {
                var totp = CreateTotp(base32Secret);
                var window = new VerificationWindow(previous: _allowedDriftSteps, future: _allowedDriftSteps);
                return totp.VerifyTotp(code, out matchedStep, window);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Failed to validate TOTP due to invalid secret format.");
                return false;
            }
        }

        private Totp CreateTotp(string base32Secret)
        {
            var secretBytes = Base32Encoding.ToBytes(base32Secret);
            return new Totp(secretBytes, (int)_timeStep.TotalSeconds, OtpHashMode.Sha1, totpSize: 6);
        }
    }
}

