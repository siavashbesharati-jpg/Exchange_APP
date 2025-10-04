using Microsoft.AspNetCore.Mvc;
using ForexExchange.Services;

namespace ForexExchange.ViewComponents
{
    public class BrandingViewComponent : ViewComponent
    {
        private readonly ISettingsService _settingsService;
        private readonly IFileUploadService _fileUploadService;

        public BrandingViewComponent(ISettingsService settingsService, IFileUploadService fileUploadService)
        {
            _settingsService = settingsService;
            _fileUploadService = fileUploadService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                var websiteName = await _settingsService.GetWebsiteNameAsync();
                var companyName = await _settingsService.GetCompanyNameAsync();
                var companyWebsite = await _settingsService.GetCompanyWebsiteAsync();
                var logoPath = await _settingsService.GetLogoPathAsync();
                var logoUrl = _fileUploadService.GetLogoUrl(logoPath);

                var model = new BrandingInfo
                {
                    WebsiteName = websiteName,
                    CompanyName = companyName,
                    CompanyWebsite = companyWebsite,
                    LogoUrl = logoUrl
                };

                return View(model);
            }
            catch
            {
                // Return default values if something goes wrong
                var defaultModel = new BrandingInfo
                {
                    WebsiteName = "سامانه معاملات تابان",
                    CompanyName = "گروه تابان",
                    CompanyWebsite = "https://taban-group.com",
                    LogoUrl = "/favicon/android-chrome-512x512.png"
                };

                return View(defaultModel);
            }
        }
    }

    public class BrandingInfo
    {
        public string WebsiteName { get; set; } = "سامانه معاملات تابان";
        public string CompanyName { get; set; } = "گروه تابان";
        public string CompanyWebsite { get; set; } = "https://taban-group.com";
        public string LogoUrl { get; set; } = "/favicon/android-chrome-512x512.png";
    }
}