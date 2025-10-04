using System.ComponentModel.DataAnnotations;

namespace ForexExchange.Models
{
    public class BrandingSettingsViewModel
    {
        [Required(ErrorMessage = "نام وب‌سایت الزامی است")]
        [Display(Name = "نام وب‌سایت")]
        [StringLength(100, ErrorMessage = "نام وب‌سایت نمی‌تواند بیش از 100 کاراکتر باشد")]
        public string WebsiteName { get; set; } = "سامانه معاملات تابان";

        [Required(ErrorMessage = "نام شرکت الزامی است")]
        [Display(Name = "نام شرکت")]
        [StringLength(100, ErrorMessage = "نام شرکت نمی‌تواند بیش از 100 کاراکتر باشد")]
        public string CompanyName { get; set; } = "گروه تابان";

        [Required(ErrorMessage = "آدرس وب‌سایت الزامی است")]
        [Display(Name = "وب‌سایت شرکت")]
        [StringLength(200, ErrorMessage = "آدرس وب‌سایت نمی‌تواند بیش از 200 کاراکتر باشد")]
        [Url(ErrorMessage = "لطفاً آدرس وب‌سایت معتبر وارد کنید")]
        public string CompanyWebsite { get; set; } = "https://taban-group.com";

        [Display(Name = "فایل لوگو")]
        public IFormFile? LogoFile { get; set; }

        // For display purposes
        public string? CurrentLogoPath { get; set; }
        public string LogoUrl { get; set; } = "/favicon/android-chrome-512x512.png";
    }
}