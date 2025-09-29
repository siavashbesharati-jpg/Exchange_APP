using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ForexExchange.Models;

namespace ForexExchange.Extensions
{
    /// <summary>
    /// Extension methods for enums
    /// متدهای توسعه برای enumها
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// Get display name from Display attribute for any enum
        /// دریافت نام نمایشی از ویژگی Display برای هر enum
        /// </summary>
        public static string GetDisplayName<TEnum>(this TEnum enumValue) where TEnum : Enum
        {
            var memberInfo = enumValue.GetType().GetMember(enumValue.ToString()).FirstOrDefault();
            if (memberInfo != null)
            {
                var displayAttribute = memberInfo.GetCustomAttribute<DisplayAttribute>();
                if (displayAttribute != null)
                {
                    return displayAttribute.Name ?? enumValue.ToString();
                }
            }
            return enumValue.ToString();
        }
    }
}
