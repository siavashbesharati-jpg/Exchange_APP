namespace ForexExchange.Services
{
    public interface IFileUploadService
    {
        Task<(bool Success, string? FilePath, string? Error)> UploadLogoAsync(IFormFile file, string currentUser = "Admin");
        Task<bool> DeleteFileAsync(string filePath);
        bool IsValidImageFile(IFormFile file);
        string GetLogoUrl(string? logoPath);
    }

    public class FileUploadService : IFileUploadService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileUploadService> _logger;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
        private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

        public FileUploadService(IWebHostEnvironment environment, ILogger<FileUploadService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<(bool Success, string? FilePath, string? Error)> UploadLogoAsync(IFormFile file, string currentUser = "Admin")
        {
            try
            {
                // Validate file
                if (!IsValidImageFile(file))
                {
                    return (false, null, "فایل انتخاب شده معتبر نیست. لطفاً تصویری با فرمت JPG، PNG، GIF، BMP یا WebP انتخاب کنید.");
                }

                if (file.Length > MaxFileSizeBytes)
                {
                    return (false, null, $"حجم فایل نمی‌تواند بیش از {MaxFileSizeBytes / (1024 * 1024)} مگابایت باشد.");
                }

                // Create uploads directory if it doesn't exist
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "logos");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                // Generate unique filename
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileName = $"logo_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsPath, fileName);
                var webPath = $"/uploads/logos/{fileName}";

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                _logger.LogInformation($"Logo uploaded successfully by {currentUser}: {webPath}");
                return (true, webPath, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading logo by {currentUser}");
                return (false, null, "خطا در آپلود فایل. لطفاً دوباره تلاش کنید.");
            }
        }

        public Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return Task.FromResult(true);

                var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
                
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation($"File deleted successfully: {filePath}");
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting file: {filePath}");
                return Task.FromResult(false);
            }
        }

        public bool IsValidImageFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            // Check file extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
                return false;

            // Check MIME type
            var mimeType = file.ContentType.ToLowerInvariant();
            var allowedMimeTypes = new[] 
            { 
                "image/jpeg", "image/jpg", "image/png", "image/gif", 
                "image/bmp", "image/webp" 
            };

            return allowedMimeTypes.Contains(mimeType);
        }

        public string GetLogoUrl(string? logoPath)
        {
            if (string.IsNullOrEmpty(logoPath))
            {
                // Return default logo path
                return "/favicon/android-chrome-512x512.png";
            }

            // Check if file exists
            var fullPath = Path.Combine(_environment.WebRootPath, logoPath.TrimStart('/'));
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning($"Logo file not found: {logoPath}, using default");
                return "/favicon/android-chrome-512x512.png";
            }

            return logoPath;
        }
    }
}