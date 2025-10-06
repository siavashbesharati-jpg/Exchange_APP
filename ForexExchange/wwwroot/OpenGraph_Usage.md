# Open Graph Tags Usage Guide

## What's Been Added

We've added comprehensive Open Graph meta tags to improve how your website appears when shared on WhatsApp, Facebook, Twitter, and other social media platforms.

## Default Tags in _Layout.cshtml

The following Open Graph tags are automatically included on every page:

- `og:title` - Page title (uses ViewData["OGTitle"] or ViewData["Title"])
- `og:description` - Page description 
- `og:image` - Preview image
- `og:url` - Current page URL
- `og:type` - Content type (usually "website")
- `og:site_name` - Site name
- `og:locale` - Language locale (fa_IR for Persian)

## How to Customize for Specific Pages

In any view file, add these in the `@{}` section:

```csharp
@{
    ViewData["Title"] = "صفحه شما";
    
    // Custom Open Graph tags
    ViewData["OGTitle"] = "عنوان ویژه برای شبکه‌های اجتماعی";
    ViewData["OGDescription"] = "توضیحات جذاب برای نمایش در واتس‌اپ و شبکه‌های اجتماعی";
    ViewData["OGImage"] = Url.Content("~/images/custom-page-image.jpg");
    ViewData["OGImageAlt"] = "متن جایگزین تصویر";
    ViewData["OGType"] = "article"; // or "product", "website", etc.
}
```

## Image Requirements

For best results on WhatsApp and social media:

- **Size**: 1200x630 pixels (recommended)
- **Minimum**: 600x315 pixels
- **Maximum file size**: 8MB
- **Format**: JPG, PNG, or GIF
- **Aspect ratio**: 1.91:1

## Example for Different Page Types

### Product/Service Page
```csharp
ViewData["OGTitle"] = "خدمات تبدیل ارز - سامانه اکسورا";
ViewData["OGDescription"] = "تبدیل آنلاین ارز با بهترین نرخ‌ها. دلار، یورو، پوند و سایر ارزها";
ViewData["OGType"] = "product";
```

### Article/News Page
```csharp
ViewData["OGTitle"] = "آخرین اخبار بازار ارز";
ViewData["OGDescription"] = "تحلیل‌ها و پیش‌بینی‌های بازار ارز ایران";
ViewData["OGType"] = "article";
```

### Contact Page
```csharp
ViewData["OGTitle"] = "تماس با سامانه اکسورا";
ViewData["OGDescription"] = "راه‌های ارتباط با تیم پشتیبانی سامانه معاملات اکسورا";
ViewData["OGType"] = "website";
```

## Testing Your Open Graph Tags

1. **WhatsApp**: Share a link to your page in WhatsApp
2. **Facebook Debugger**: https://developers.facebook.com/tools/debug/
3. **Twitter Card Validator**: https://cards-dev.twitter.com/validator
4. **LinkedIn Post Inspector**: https://www.linkedin.com/post-inspector/

## Notes

- Place a default og-image.jpg in `/wwwroot/images/` folder
- The system will automatically fall back to favicon if no custom image is set
- All text should be in Persian for Persian audience
- Images are cached by social platforms, so changes may take time to appear
