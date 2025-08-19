using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class OptionalEmailAttribute : ValidationAttribute
    {
        public OptionalEmailAttribute()
        {
            ErrorMessage = "فرمت ایمیل صحیح نیست";
        }

        public override bool IsValid(object? value)
        {
            // If value is null or empty, it's valid (optional)
            if (value == null || (value is string email && string.IsNullOrWhiteSpace(email)))
            {
                return true;
            }

            // If value exists, validate email format
            if (value is string emailValue)
            {
                var emailAttribute = new EmailAddressAttribute();
                return emailAttribute.IsValid(emailValue);
            }

            return false;
        }
    }
}
