using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Services
{
    public interface ISettingsService
    {
        Task<T> GetSettingAsync<T>(string key, T defaultValue = default);
        Task SetSettingAsync<T>(string key, T value, string? description = null, string updatedBy = "System");
        Task<SystemSettingsViewModel> GetSystemSettingsAsync();
        Task UpdateSystemSettingsAsync(SystemSettingsViewModel settings, string updatedBy = "Admin");
        Task<decimal> GetCommissionRateAsync();
        Task<decimal> GetExchangeFeeRateAsync();
        Task<decimal> GetMinTransactionAmountAsync();
        Task<decimal> GetMaxTransactionAmountAsync();
        Task<decimal> GetDailyTransactionLimitAsync();
        Task<bool> IsSystemMaintenanceAsync();
        
        // Website Branding Methods
        Task<string> GetWebsiteNameAsync();
        Task<string> GetCompanyNameAsync();
        Task<string> GetCompanyWebsiteAsync();
        Task<string?> GetLogoPathAsync();
        Task SetBrandingAsync(string websiteName, string companyName, string companyWebsite, string? logoPath = null, string updatedBy = "Admin");
    }

    public class SettingsService : ISettingsService
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<SettingsService> _logger;

        public SettingsService(ForexDbContext context, ILogger<SettingsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<T> GetSettingAsync<T>(string key, T defaultValue = default)
        {
            try
            {
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == key && s.IsActive);

                if (setting == null)
                {
                    _logger.LogWarning($"Setting '{key}' not found, returning default value");
                    return defaultValue!; // ensure non-null for reference types
                }

                return ConvertToType<T>(setting.SettingValue, defaultValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting setting '{key}', returning default value");
                return defaultValue!; // ensure non-null for reference types
            }
        }

        public async Task SetSettingAsync<T>(string key, T value, string? description = null, string updatedBy = "System")
        {
            try
            {
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.SettingKey == key);

                if (setting == null)
                {
                    setting = new SystemSettings
                    {
                        SettingKey = key,
                        SettingValue = value?.ToString() ?? string.Empty,
                        Description = description,
                        DataType = GetDataType<T>(),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        UpdatedBy = updatedBy,
                        IsActive = true
                    };
                    _context.SystemSettings.Add(setting);
                }
                else
                {
                    setting.SettingValue = value?.ToString() ?? string.Empty;
                    setting.UpdatedAt = DateTime.Now;
                    setting.UpdatedBy = updatedBy;
                    if (!string.IsNullOrEmpty(description))
                    {
                        setting.Description = description;
                    }
                    _context.SystemSettings.Update(setting);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Setting '{key}' updated to '{value}' by {updatedBy}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting '{key}' to '{value}'");
                throw;
            }
        }

        public async Task<SystemSettingsViewModel> GetSystemSettingsAsync()
        {
            return new SystemSettingsViewModel
            {
                CommissionRate = await GetSettingAsync(SettingKeys.CommissionRate, 0.5m),
                ExchangeFeeRate = await GetSettingAsync(SettingKeys.ExchangeFeeRate, 0.2m),
                MinTransactionAmount = await GetSettingAsync(SettingKeys.MinTransactionAmount, 10000m),
                MaxTransactionAmount = await GetSettingAsync(SettingKeys.MaxTransactionAmount, 1000000000m),
                DailyTransactionLimit = await GetSettingAsync(SettingKeys.DailyTransactionLimit, 5000000000m),
                SystemMaintenance = await GetSettingAsync(SettingKeys.SystemMaintenance, false),
                DefaultCurrencyCode = await GetSettingAsync(SettingKeys.DefaultCurrency, "USD"),
                RateUpdateInterval = await GetSettingAsync(SettingKeys.RateUpdateInterval, 60),
                NotificationEnabled = await GetSettingAsync(SettingKeys.NotificationEnabled, true),
                BackupEnabled = await GetSettingAsync(SettingKeys.BackupEnabled, true),
                
                // Website Branding Settings
                WebsiteName = await GetSettingAsync(SettingKeys.WebsiteName, "سامانه معاملات تابان"),
                CompanyName = await GetSettingAsync(SettingKeys.CompanyName, "گروه تابان"),
                CompanyWebsite = await GetSettingAsync(SettingKeys.CompanyWebsite, "https://taban-group.com"),
                LogoPath = await GetSettingAsync<string?>(SettingKeys.WebsiteLogoPath, null)
            };
        }

        public async Task UpdateSystemSettingsAsync(SystemSettingsViewModel settings, string updatedBy = "Admin")
        {
            await SetSettingAsync(SettingKeys.CommissionRate, settings.CommissionRate, "نرخ کمیسیون به درصد", updatedBy);
            await SetSettingAsync(SettingKeys.ExchangeFeeRate, settings.ExchangeFeeRate, "کارمزد تبدیل ارز به درصد", updatedBy);
            await SetSettingAsync(SettingKeys.MinTransactionAmount, settings.MinTransactionAmount, "حداقل مبلغ تراکنش به تومان", updatedBy);
            await SetSettingAsync(SettingKeys.MaxTransactionAmount, settings.MaxTransactionAmount, "حداکثر مبلغ تراکنش به تومان", updatedBy);
            await SetSettingAsync(SettingKeys.DailyTransactionLimit, settings.DailyTransactionLimit, "محدودیت تراکنش روزانه به تومان", updatedBy);
            await SetSettingAsync(SettingKeys.SystemMaintenance, settings.SystemMaintenance, "حالت تعمیرات سیستم", updatedBy);
            await SetSettingAsync(SettingKeys.DefaultCurrency, settings.DefaultCurrencyCode, "کد ارز پیش‌فرض سیستم", updatedBy);
            await SetSettingAsync(SettingKeys.RateUpdateInterval, settings.RateUpdateInterval, "بازه بروزرسانی نرخ ارز به دقیقه", updatedBy);
            await SetSettingAsync(SettingKeys.NotificationEnabled, settings.NotificationEnabled, "فعال‌سازی سیستم اعلان‌ها", updatedBy);
            await SetSettingAsync(SettingKeys.BackupEnabled, settings.BackupEnabled, "فعال‌سازی پشتیبان‌گیری خودکار", updatedBy);
            
            // Update Website Branding Settings
            await SetSettingAsync(SettingKeys.WebsiteName, settings.WebsiteName, "نام وب‌سایت", updatedBy);
            await SetSettingAsync(SettingKeys.CompanyName, settings.CompanyName, "نام شرکت", updatedBy);
            await SetSettingAsync(SettingKeys.CompanyWebsite, settings.CompanyWebsite, "وب‌سایت شرکت", updatedBy);
            
            if (!string.IsNullOrEmpty(settings.LogoPath))
            {
                await SetSettingAsync(SettingKeys.WebsiteLogoPath, settings.LogoPath, "مسیر لوگو وب‌سایت", updatedBy);
            }
        }

        public async Task<decimal> GetCommissionRateAsync()
        {
            var rate = await GetSettingAsync(SettingKeys.CommissionRate, 0.5m);
            return rate / 100m; // Convert percentage to decimal
        }

        public async Task<decimal> GetExchangeFeeRateAsync()
        {
            var rate = await GetSettingAsync(SettingKeys.ExchangeFeeRate, 0.2m);
            return rate / 100m; // Convert percentage to decimal
        }

        public async Task<decimal> GetMinTransactionAmountAsync()
        {
            return await GetSettingAsync(SettingKeys.MinTransactionAmount, 10000m);
        }

        public async Task<decimal> GetMaxTransactionAmountAsync()
        {
            return await GetSettingAsync(SettingKeys.MaxTransactionAmount, 1000000000m);
        }

        public async Task<decimal> GetDailyTransactionLimitAsync()
        {
            return await GetSettingAsync(SettingKeys.DailyTransactionLimit, 5000000000m);
        }

        public async Task<bool> IsSystemMaintenanceAsync()
        {
            return await GetSettingAsync(SettingKeys.SystemMaintenance, false);
        }

        private T ConvertToType<T>(string value, T defaultValue)
        {
            try
            {
                if (string.IsNullOrEmpty(value))
                    return defaultValue;

                if (typeof(T) == typeof(string))
                    return (T)(object)value;

                if (typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?))
                    return (T)(object)decimal.Parse(value);

                if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
                    return (T)(object)int.Parse(value);

                if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                    return (T)(object)bool.Parse(value);

                if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
                    return (T)(object)DateTime.Parse(value);

                // Try to convert using Convert.ChangeType for other types
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error converting '{value}' to type {typeof(T).Name}");
                return defaultValue;
            }
        }

        private string GetDataType<T>()
        {
            if (typeof(T) == typeof(string))
                return "string";
            if (typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?))
                return "decimal";
            if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
                return "int";
            if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                return "bool";
            if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
                return "datetime";

            return "string";
        }

        // Website Branding Methods Implementation
        public async Task<string> GetWebsiteNameAsync()
        {
            return await GetSettingAsync(SettingKeys.WebsiteName, "سامانه معاملات تابان");
        }

        public async Task<string> GetCompanyNameAsync()
        {
            return await GetSettingAsync(SettingKeys.CompanyName, "گروه تابان");
        }

        public async Task<string> GetCompanyWebsiteAsync()
        {
            return await GetSettingAsync(SettingKeys.CompanyWebsite, "https://taban-group.com");
        }

        public async Task<string?> GetLogoPathAsync()
        {
            return await GetSettingAsync<string?>(SettingKeys.WebsiteLogoPath, null);
        }

        public async Task SetBrandingAsync(string websiteName, string companyName, string companyWebsite, string? logoPath = null, string updatedBy = "Admin")
        {
            try
            {
                await SetSettingAsync(SettingKeys.WebsiteName, websiteName, "نام وب‌سایت", updatedBy);
                await SetSettingAsync(SettingKeys.CompanyName, companyName, "نام شرکت", updatedBy);
                await SetSettingAsync(SettingKeys.CompanyWebsite, companyWebsite, "وب‌سایت شرکت", updatedBy);
                
                if (!string.IsNullOrEmpty(logoPath))
                {
                    await SetSettingAsync(SettingKeys.WebsiteLogoPath, logoPath, "مسیر لوگو وب‌سایت", updatedBy);
                }

                _logger.LogInformation($"Website branding updated by {updatedBy}: Website={websiteName}, Company={companyName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating website branding by {updatedBy}");
                throw;
            }
        }
    }
}
