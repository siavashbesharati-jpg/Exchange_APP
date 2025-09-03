using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
        [Display(Name = "نام و نام خانوادگی")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "ایمیل (اختیاری)")]
        public string? Email { get; set; }

        [Display(Name = "شماره تلفن")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "رمز عبور")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "تکرار رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "تکرار رمز عبور")]
        [Compare("Password", ErrorMessage = "رمز عبور و تکرار آن باید یکسان باشند")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "کد ملی")]
        public string? NationalId { get; set; }

        [Display(Name = "آدرس")]
        public string? Address { get; set; }
    }

    public class LoginViewModel
    {
        [Display(Name = "شماره تلفن")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "رمز عبور")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "مرا به خاطر بسپار")]
        public bool RememberMe { get; set; }
    }

    public class ProfileViewModel
    {
        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
        [Display(Name = "نام و نام خانوادگی")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "ایمیل")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "کد ملی")]
        public string? NationalId { get; set; }

        [Display(Name = "آدرس")]
        public string? Address { get; set; }

        [Display(Name = "نقش کاربری")]
        public string Role { get; set; } = string.Empty;
    }

    public class CustomerCreateViewModel
    {
        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
        [Display(Name = "نام و نام خانوادگی")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "ایمیل (اختیاری)")]
       
        public string? Email { get; set; }

        [Display(Name = "شماره تلفن")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "رمز عبور")]
        [StringLength(100, ErrorMessage = "رمز عبور باید حداقل {2} کاراکتر باشد", MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "تکرار رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "تکرار رمز عبور")]
        [Compare("Password", ErrorMessage = "رمز عبور و تکرار آن باید یکسان باشند")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "کد ملی")]
        [StringLength(10, ErrorMessage = "کد ملی باید 10 رقم باشد", MinimumLength = 10)]
        public string? NationalId { get; set; }

        [Display(Name = "آدرس")]
        public string? Address { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; } = true;

    // Initial balances per currency (code -> amount). Allow negative and positive.
    public Dictionary<string, decimal> InitialBalances { get; set; } = new();
    }

    public class CustomerEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
        [Display(Name = "نام و نام خانوادگی")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "ایمیل (اختیاری)")]
        public string? Email { get; set; }

        [Display(Name = "شماره تلفن")]
        public string PhoneNumber { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "رمز عبور جدید (اختیاری)")]
        [StringLength(100, ErrorMessage = "رمز عبور باید حداقل {2} کاراکتر باشد", MinimumLength = 6)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "تکرار رمز عبور جدید")]
        [Compare("NewPassword", ErrorMessage = "رمز عبور و تکرار آن باید یکسان باشند")]
        public string? ConfirmNewPassword { get; set; }

        [Display(Name = "کد ملی")]
        [StringLength(10, ErrorMessage = "کد ملی باید 10 رقم باشد", MinimumLength = 10)]
        public string? NationalId { get; set; }

        [Display(Name = "آدرس")]
        public string? Address { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

    // Initial balances per currency (code -> amount). Allow negative and positive.
    public Dictionary<string, decimal> InitialBalances { get; set; } = new();
    }

    public class DatabaseManagementViewModel
    {
        public int CustomersCount { get; set; }
        public int OrdersCount { get; set; }
        public int CurrencyPoolsCount { get; set; }
        public int TransactionsCount { get; set; }
        public int ExchangeRatesCount { get; set; }
        public int ReceiptsCount { get; set; }
    }
}
