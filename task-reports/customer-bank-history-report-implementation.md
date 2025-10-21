# گزارش تکمیل: گزارش تاریخچه بانک و مشتری

**تاریخ**: 2025-10-19  
**وضعیت**: ✅ تکمیل شده

## خلاصه کار انجام شده

یک گزارش جدید مشابه "گزارش روزانه بانک و مشتری" ایجاد شد با قابلیت فیلتر بازه زمانی (از تاریخ - تا تاریخ).

## فایل‌های ایجاد/تغییر داده شده

### 1. فایل‌های مدل (ViewModel)
✅ **ViewModel موجود بود**: `ForexExchange/Models/CustomerBankHistoryReportViewModel.cs`
- شامل کلاس‌های:
  - `CustomerBankHistoryReportViewModel` (مدل اصلی گزارش)
  - `CustomerBankHistoryCurrencyViewModel` (اطلاعات هر ارز)
  - `CustomerBankHistoryBankDetailViewModel` (جزئیات بانک‌ها)
  - `CustomerBankHistoryCustomerDetailViewModel` (جزئیات مشتریان)
  - `CustomerBankHistorySummaryConversionViewModel` (خلاصه تبدیل شده)

### 2. فایل‌های کنترلر
✅ **متدهای موجود بودند**: `ForexExchange/Controllers/ReportsController.cs`

متدهای Controller که قبلاً پیاده‌سازی شده بودند:
- `CustomerBankHistoryReport()` - نمایش View
- `GetCustomerBankHistoryReport(DateTime dateFrom, DateTime dateTo, string? currencyCode)` - دریافت داده‌های JSON
- `CustomerBankHistoryReportPrint(DateTime dateFrom, DateTime dateTo, string? currencyCode)` - نمایش نسخه قابل چاپ
- `ExportCustomerBankHistoryReportToExcel(DateTime dateFrom, DateTime dateTo, string? currencyCode)` - خروجی Excel
- `BuildCustomerBankHistoryReportAsync(DateTime dateFrom, DateTime dateTo, string? preferredCurrencyCode)` - ساخت گزارش

**ویژگی‌های فیلتر تاریخ:**
```csharp
// تنظیم محدوده زمانی به صورت دقیق
startDateTime = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0);  // >= 00:00:00
endDateTime = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59);      // <= 23:59:59

// استفاده از >= و <= در کوئری
.Where(h => !h.IsDeleted && h.TransactionDate >= startDateTime && h.TransactionDate <= endDateTime)
```

### 3. فایل‌های سرویس Excel
✅ **متد موجود بود**: `ForexExchange/Services/ExcelExportService.cs`
- `GenerateCustomerBankHistoryReportExcel(CustomerBankHistoryReportViewModel report)`

### 4. فایل‌های View
✅ **جدید**: `ForexExchange/Views/Reports/CustomerBankHistoryReport.cshtml`
- فیلتر از تاریخ و تا تاریخ
- دکمه‌های میانبر: امروز، دیروز، این هفته، هفته گذشته، این ماه، ماه گذشته
- نمایش خلاصه کل با قابلیت تغییر ارز مرجع
- نمایش جزئیات هر ارز با جداول بانک‌ها و مشتریان
- دکمه‌های چاپ و خروجی Excel

✅ **جدید**: `ForexExchange/Views/PrintViews/CustomerBankHistoryReportPrint.cshtml`
- نسخه قابل چاپ گزارش
- طراحی مشابه گزارش روزانه
- نمایش بازه زمانی (از تاریخ - تا تاریخ)

### 5. صفحه اصلی گزارشات
✅ **تغییر**: `ForexExchange/Views/Reports/Index.cshtml`
- افزودن کارت "گزارش تاریخچه بانک و مشتری"
- استایل gradient بنفش-صورتی
- مسیریابی به `customerbankhistory`

## ویژگی‌های کلیدی

### 1. فیلتر بازه زمانی
- **از تاریخ**: فیلتر با `>=` از ساعت 00:00:00
- **تا تاریخ**: فیلتر با `<=` تا ساعت 23:59:59
- **بدون تأثیر زمان**: فقط تاریخ ملاک است نه ساعت

### 2. دکمه‌های میانبر
```javascript
- امروز
- دیروز
- این هفته (شنبه تا امروز)
- هفته گذشته
- این ماه
- ماه گذشته
```

### 3. نمایش داده‌ها
- تفکیک به ارز
- مجموع موجودی بانک‌ها
- مجموع موجودی مشتریان
- تراز (اختلاف)
- جزئیات هر بانک
- جزئیات هر مشتری

### 4. خلاصه کل
- انتخاب ارز مرجع
- تبدیل تمام ارزها به ارز انتخابی
- هشدار در صورت نبود نرخ معتبر

### 5. خروجی‌ها
- نمایش صفحه‌ای
- چاپ PDF
- خروجی Excel

## نحوه استفاده

### دسترسی به گزارش
1. منوی گزارشات → گزارش تاریخچه بانک و مشتری
2. یا مستقیماً: `/Reports/CustomerBankHistoryReport`

### انتخاب بازه زمانی
1. انتخاب دستی: از تاریخ و تا تاریخ را وارد کنید
2. میانبر: روی یکی از دکمه‌های میانبر کلیک کنید

### نمایش گزارش
- کلیک روی دکمه "جستجو"
- گزارش به صورت Ajax بارگذاری می‌شود

### تغییر ارز مرجع
- در بخش خلاصه، ارز مرجع را از dropdown انتخاب کنید
- مجموع‌ها به صورت خودکار محاسبه می‌شوند

### خروجی
- **چاپ**: دکمه "چاپ گزارش"
- **Excel**: دکمه "خروجی اکسل"

## تفاوت با گزارش روزانه

| ویژگی | گزارش روزانه | گزارش تاریخچه |
|-------|-------------|---------------|
| فیلتر تاریخ | یک تاریخ | بازه (از-تا) |
| میانبرها | روز قبل/بعد، امروز | امروز، دیروز، این هفته/ماه، ... |
| پیش‌فرض | امروز | 30 روز اخیر |
| Layout | col-lg-6 | col-lg-4 |

## تست‌ها

### ✅ Build موفق
```
Build succeeded with 38 warning(s) in 39.7s
```

### تست‌های پیشنهادی
1. انتخاب بازه زمانی مختلف
2. استفاده از دکمه‌های میانبر
3. تغییر ارز مرجع در خلاصه
4. چاپ گزارش
5. دانلود Excel
6. بررسی صحت محاسبات

## نکات فنی

### فیلتر زمان دقیق
```csharp
// از تاریخ: شامل کل روز از ساعت 00:00:00
var startDateTime = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0);

// تا تاریخ: شامل کل روز تا ساعت 23:59:59
var endDateTime = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59);
```

### Query با >= و <=
```csharp
.Where(h => !h.IsDeleted && 
            h.TransactionDate >= startDateTime && 
            h.TransactionDate <= endDateTime)
```

### کارایی
- استفاده از `GroupBy` برای یافتن آخرین موجودی
- `OrderByDescending` برای تضمین آخرین رکورد
- فیلتر `!h.IsDeleted` برای حذف رکوردهای soft-deleted

## مستندات مرتبط

- الگوی گزارش روزانه: `Views/Reports/CustomerBankDailyReport.cshtml`
- الگوی چاپ روزانه: `Views/PrintViews/CustomerBankDailyReportPrint.cshtml`
- سرویس تبدیل ارز: `ICurrencyConversionService`
- سرویس Excel: `ExcelExportService`

---

**یادداشت**: تمام فایل‌های مورد نیاز از قبل موجود بودند و فقط View ها و لینک در صفحه اصلی گزارشات اضافه شد.
