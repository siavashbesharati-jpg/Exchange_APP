using ForexExchange.Services;

namespace ForexExchange.Tests
{
    /// <summary>
    /// Phone Number Service Test Examples
    /// نمونه تست‌های سرویس شماره تلفن
    /// </summary>
    public class PhoneNumberServiceTests
    {
        public static void RunTests()
        {
            Console.WriteLine("=== Phone Number Normalization Tests ===");
            Console.WriteLine();

            // Test cases
            var testCases = new[]
            {
                // Iranian numbers
                ("0912345678", "0098912345678"),
                ("09121234567", "0098912234567"),
                ("+989121234567", "0098912234567"),
                ("00989121234567", "0098912234567"),
                ("9121234567", "0098912234567"),

                // Indian numbers
                ("+911234567890", "0091234567890"),
                ("001234567890", "001234567890"),
                ("01234567890", "009812347890"), // Treated as Iranian due to leading 0

                // US numbers
                ("+12345678901", "0012345678901"),
                ("0012345678901", "0012345678901"),

                // Invalid cases
                ("", ""),
                ("abc", "0098abc"),
                ("+", "00"),
            };

            foreach (var (input, expected) in testCases)
            {
                var result = PhoneNumberService.NormalizePhoneNumber(input);
                var isValid = PhoneNumberService.IsValidNormalizedPhoneNumber(result);
                var display = PhoneNumberService.GetDisplayFormat(result);
                
                Console.WriteLine($"Input: '{input}'");
                Console.WriteLine($"  Normalized: '{result}'");
                Console.WriteLine($"  Expected: '{expected}'");
                Console.WriteLine($"  Valid: {isValid}");
                Console.WriteLine($"  Display: '{display}'");
                Console.WriteLine($"  Match: {(result == expected ? "✓" : "✗")}");
                Console.WriteLine();
            }

            Console.WriteLine("=== Iranian Phone Number Validation Tests ===");
            Console.WriteLine();

            var iranianTests = new[]
            {
                ("0912345678", true),
                ("+989121234567", true),
                ("00989121234567", true),
                ("+911234567890", false),
                ("invalid", false),
            };

            foreach (var (input, expectedValid) in iranianTests)
            {
                var isValid = PhoneNumberService.IsValidIranianPhoneNumber(input);
                Console.WriteLine($"Input: '{input}' - Valid Iranian: {isValid} (Expected: {expectedValid}) {(isValid == expectedValid ? "✓" : "✗")}");
            }
        }
    }
}
