using Microsoft.AspNetCore.Mvc;
using ForexExchange.Services;

namespace ForexExchange.ViewComponents
{
    public class BrandingViewComponent : ViewComponent
    {
        private readonly ISettingsService _settingsService;

        public BrandingViewComponent(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                var websiteName = await _settingsService.GetWebsiteNameAsync();
                var companyName = await _settingsService.GetCompanyNameAsync();
                var companyWebsite = await _settingsService.GetCompanyWebsiteAsync();
                var logoDataUrl = await _settingsService.GetLogoDataUrlAsync();

                var model = new BrandingInfo
                {
                    WebsiteName = websiteName,
                    CompanyName = companyName,
                    CompanyWebsite = companyWebsite,
                    LogoUrl = logoDataUrl
                };

                return View(model);
            }
            catch
            {
                // Return default values if something goes wrong
                var defaultModel = new BrandingInfo
                {
                    WebsiteName = "سامانه معاملات اکسورا",
                    CompanyName = "گروه اکسورا",
                    CompanyWebsite = "https://Exsora.iranexpedia.ir",
                    LogoUrl = "/favicon/android-chrome-512x512.png"
                };

                return View(defaultModel);
            }
        }
    }

    public class BrandingInfo
    {
        public string WebsiteName { get; set; } = "سامانه معاملات اکسورا";
        public string CompanyName { get; set; } = "گروه اکسورا";
        public string CompanyWebsite { get; set; } = "https://Exsora.iranexpedia.ir";
        public string LogoUrl { get; set; } = "/favicon/android-chrome-512x512.png";
    }
}