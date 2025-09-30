using System.Text.RegularExpressions;

namespace ForexExchange.Services
{
    /// <summary>
    /// Phone Number Normalization Service
    /// سرویس نرمال‌سازی شماره تلفن
    /// </summary>
    public class PhoneNumberService
    {
        private const string IRAN_COUNTRY_CODE = "0098";
        private const string IRAN_DOMESTIC_PREFIX = "0";

        /// <summary>
        /// Normalize phone number to international format with country code
        /// نرمال‌سازی شماره تلفن به فرمت بین‌المللی با کد کشور
        /// </summary>
        /// <param name="phoneNumber">Input phone number</param>
        /// <param name="defaultCountryCode">Default country code if not provided (default: Iran 0098)</param>
        /// <returns>Normalized phone number in format 00XX...</returns>
        public static string NormalizePhoneNumber(string phoneNumber, string defaultCountryCode = IRAN_COUNTRY_CODE)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return string.Empty;

            // Remove all non-digit characters except +
            string cleaned = Regex.Replace(phoneNumber.Trim(), @"[^\d+]", "");
            
            // Handle different input formats
            if (cleaned.StartsWith("+"))
            {
                // Format: +91xxxxxxxxx -> 0091xxxxxxxxx
                string withoutPlus = cleaned.Substring(1);
                return "00" + withoutPlus;
            }
            else if (cleaned.StartsWith("00"))
            {
                // Already in correct format: 0091xxxxxxxxx
                return cleaned;
            }
            else if (cleaned.StartsWith("0") && cleaned.Length > 1)
            {
                // Iranian domestic format: 0912xxxxxx -> 0098912xxxxxx
                if (defaultCountryCode == IRAN_COUNTRY_CODE)
                {
                    return IRAN_COUNTRY_CODE + cleaned.Substring(1);
                }
                else
                {
                    // For other countries, remove leading 0 and add country code
                    return defaultCountryCode + cleaned.Substring(1);
                }
            }
            else
            {
                // No country code or leading zero: 912xxxxxx -> 0098912xxxxxx
                return defaultCountryCode + cleaned;
            }
        }

        /// <summary>
        /// Validate if phone number is in correct format after normalization
        /// اعتبارسنجی شماره تلفن پس از نرمال‌سازی
        /// </summary>
        /// <param name="normalizedPhoneNumber">Normalized phone number</param>
        /// <returns>True if valid</returns>
        public static bool IsValidNormalizedPhoneNumber(string? normalizedPhoneNumber)
        {
            if (string.IsNullOrWhiteSpace(normalizedPhoneNumber))
                return false;

            // Must start with "00" and be followed only by digits.
            if (!Regex.IsMatch(normalizedPhoneNumber, @"^00\d+$"))
                return false;

            // Minimum length: 00 + 1-digit country code + 4-digit number = 7
            // Maximum length: 00 + 3-digit country code + 15-digit number = 20
            return normalizedPhoneNumber.Length >= 7 && normalizedPhoneNumber.Length <= 20;
        }

        /// <summary>
        /// Get display format for phone number (for UI display)
        /// دریافت فرمت نمایشی شماره تلفن
        /// </summary>
        /// <param name="normalizedPhoneNumber">Normalized phone number</param>
        /// <returns>Display format</returns>
        public static string GetDisplayFormat(string normalizedPhoneNumber)
        {
            if (string.IsNullOrWhiteSpace(normalizedPhoneNumber) || !normalizedPhoneNumber.StartsWith("00"))
                return normalizedPhoneNumber;

            // Convert 00... to +...
            return "+" + normalizedPhoneNumber.Substring(2);
        }

        /// <summary>
        /// Get formatted display for phone number with proper spacing for Persian/Iranian numbers
        /// دریافت فرمت نمایشی با فاصله‌گذاری مناسب برای شماره‌های ایرانی
        /// </summary>
        /// <param name="normalizedPhoneNumber">Normalized phone number</param>
        /// <returns>Formatted display (e.g., "+98 912 067 4032")</returns>
        public static string GetFormattedDisplayFormat(string normalizedPhoneNumber)
        {
            if (string.IsNullOrWhiteSpace(normalizedPhoneNumber) || !normalizedPhoneNumber.StartsWith("00"))
                return normalizedPhoneNumber;

            string withoutPrefix = normalizedPhoneNumber.Substring(2); // Remove "00"
            
            // Check if it's Iranian number (starts with 98)
            if (withoutPrefix.StartsWith("98") && withoutPrefix.Length >= 12)
            {
                string countryCode = withoutPrefix.Substring(0, 2); // "98"
                string localNumber = withoutPrefix.Substring(2); // Remove country code
                
                // Format Iranian mobile numbers: +98 912 067 4032
                if (localNumber.StartsWith("9") && localNumber.Length == 10)
                {
                    return $"+{countryCode} {localNumber.Substring(0, 3)} {localNumber.Substring(3, 3)} {localNumber.Substring(6, 4)}";
                }
                // Format Iranian landline numbers differently based on city
                else if (localNumber.Length >= 8)
                {
                    // Most Iranian landlines: +98 21 1234 5678 (Tehran) or +98 311 123 4567 (Isfahan)
                    if (localNumber.Length == 10 && (localNumber.StartsWith("21") || localNumber.StartsWith("26")))
                    {
                        // Tehran, Karaj: +98 21 1234 5678
                        return $"+{countryCode} {localNumber.Substring(0, 2)} {localNumber.Substring(2, 4)} {localNumber.Substring(6, 4)}";
                    }
                    else if (localNumber.Length == 10)
                    {
                        // Three-digit area codes: +98 311 123 4567
                        return $"+{countryCode} {localNumber.Substring(0, 3)} {localNumber.Substring(3, 3)} {localNumber.Substring(6, 4)}";
                    }
                    else
                    {
                        // Fallback for other patterns
                        return $"+{countryCode} {localNumber}";
                    }
                }
                else
                {
                    return $"+{countryCode} {localNumber}";
                }
            }
            else
            {
                // Non-Iranian numbers - simple format with country code separation
                return "+" + withoutPrefix;
            }
        }

        /// <summary>
        /// Extract country code from normalized phone number
        /// رسید  کد کشور از شماره تلفن نرمال‌شده
        /// </summary>
        /// <param name="normalizedPhoneNumber">Normalized phone number</param>
        /// <returns>Country code (e.g., "98" for Iran)</returns>
        public static string GetCountryCode(string normalizedPhoneNumber)
        {
            if (string.IsNullOrWhiteSpace(normalizedPhoneNumber) || !normalizedPhoneNumber.StartsWith("00") || normalizedPhoneNumber.Length < 6)
                return string.Empty;

            // Try 3-digit country code first
            if (normalizedPhoneNumber.Length >= 7)
            {
                string threeDigitCode = normalizedPhoneNumber.Substring(2, 3);
                if (IsValidCountryCode(threeDigitCode))
                    return threeDigitCode;
            }

            // Fall back to 2-digit country code
            return normalizedPhoneNumber.Substring(2, 2);
        }

        /// <summary>
        /// Check if a country code is valid (basic validation)
        /// بررسی اعتبار کد کشور
        /// </summary>
        private static bool IsValidCountryCode(string countryCode)
        {
            // Common 3-digit country codes
            var threeDigitCodes = new HashSet<string> { "358", "372", "374", "375" }; // Finland, Estonia, Armenia, Belarus
            return threeDigitCodes.Contains(countryCode);
        }

        /// <summary>
        /// Validate Iranian phone number format
        /// اعتبارسنجی فرمت شماره تلفن ایرانی
        /// </summary>
        /// <param name="phoneNumber">Phone number to validate</param>
        /// <returns>True if valid Iranian number</returns>
        public static bool IsValidIranianPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            string normalized = NormalizePhoneNumber(phoneNumber);
            
            // Should be Iran country code + 10 digit number
            if (!normalized.StartsWith(IRAN_COUNTRY_CODE))
                return false;

            string localNumber = normalized.Substring(4); // Remove 0098
            
            // Iranian mobile numbers start with 9 and are 10 digits
            // Iranian landline numbers vary by city but are typically 8-11 digits
            return Regex.IsMatch(localNumber, @"^9\d{9}$") || // Mobile: 9xxxxxxxxx (10 digits)
                   Regex.IsMatch(localNumber, @"^\d{8,11}$");   // Landline: various formats
        }
    }
}
