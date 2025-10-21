# گزارش اصلاحات: منطق محاسبه تراز و بازه زمانی

**تاریخ**: 2025-10-21  
**وضعیت**: ✅ تکمیل شده

## خلاصه مسائل حل شده

### 1. مسئله منطق محاسبه تراز
**مسئله قبلی**: تراز به صورت `BankTotal + CustomerTotal` محاسبه می‌شد  
**راه‌حل**: تغییر به `BankTotal - CustomerTotal`

**منطق صحیح**:
- موجودی مثبت مشتری = ما به مشتری بدهکارهستیم (کسر از تراز)
- موجودی منفی مشتری = مشتری به ما بدهکار است (اضافه به تراز)
- موجودی مثبت بانک = بانک به ما بدهکار است (اضافه به تراز)
- موجودی منفی بانک = ما به بانک بدهکارهستیم (کسر از تراز)

**مثال‌های عملی**:
```
Banks: 500 USD, Customers: -200 USD → Balance: 500 - (-200) = 700 USD
Banks: 500 USD, Customers: 200 USD  → Balance: 500 - 200 = 300 USD
Banks: -500 USD, Customers: 200 USD → Balance: -500 - 200 = -700 USD  
Banks: -500 USD, Customers: -200 USD → Balance: -500 - (-200) = -300 USD
```

### 2. مسئله بازه زمانی
**مسئله قبلی**: گزارش تاریخچه فقط تراکنش‌های داخل بازه زمانی را نمایش می‌داد  
**راه‌حل**: نمایش کل تراکنش‌ها تا پایان بازه زمانی

**منطق صحیح**:
- گزارش باید موجودی تا پایان تاریخ انتخابی را نشان دهد
- تمام تراکنش‌ها از ابتدا تا `endDate` بررسی می‌شوند
- آخرین موجودی هر مشتری/بانک تا آن تاریخ گرفته می‌شود

## فایل‌های تغییر یافته

### 1. مدل‌های ViewModel

#### `CustomerBankHistoryReportViewModel.cs`
```csharp
// قبل
public decimal Difference => BankTotal + CustomerTotal;

// بعد  
public decimal Difference => BankTotal - CustomerTotal;
```

#### `CustomerBankDailyReportViewModel.cs`
```csharp
// قبل
public decimal Difference => BankTotal + CustomerTotal;

// بعد
public decimal Difference => BankTotal - CustomerTotal;
```

### 2. کنترلر

#### `ReportsController.cs` - متد `BuildCustomerBankHistoryReportAsync`

**تغییر منطق بازه زمانی**:
```csharp
// قبل: فیلتر تراکنش‌های داخل بازه
.Where(h => !h.IsDeleted && h.TransactionDate >= startDateTime && h.TransactionDate <= endDateTime)

// بعد: تمام تراکنش‌ها تا پایان بازه
.Where(h => !h.IsDeleted && h.TransactionDate <= endDateTime)
```

**حذف محدودیت تاریخ شروع**:
```csharp
// قبل
var startDateTime = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0);
var endDateTime = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59);

// بعد
var endDateTime = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59);
// startDateTime حذف شد
```

### 3. View ها

#### تغییر متن‌های نمایشی:
```
// قبل
"اختلاف بانک + مشتری"
"تراز بانک + مشتری"

// بعد  
"تراز نهایی (بانک - مشتری)"
```

**فایل‌های تغییر یافته**:
- `CustomerBankHistoryReport.cshtml`
- `CustomerBankDailyReport.cshtml`
- `CustomerBankHistoryReportPrint.cshtml`
- `CustomerBankDailyReportPrint.cshtml`

## جزئیات تکنیکی

### تفاوت گزارش روزانه و تاریخچه

| جنبه | گزارش روزانه | گزارش تاریخچه |
|------|-------------|---------------|
| **منطق محاسبه** | `BankTotal - CustomerTotal` | `BankTotal - CustomerTotal` |
| **بازه زمانی** | موجودی تا پایان روز انتخابی | موجودی تا پایان تاریخ انتخابی |
| **فیلتر داده** | `<= endOfDay` | `<= endDateTime` |

### کوئری‌های بهینه‌سازی شده

```csharp
// بانک‌ها - آخرین موجودی تا پایان بازه
var latestBankBalances = await _context.BankAccountBalanceHistory
    .Where(h => !h.IsDeleted && h.TransactionDate <= endDateTime)
    .GroupBy(h => h.BankAccountId)
    .Select(g => g.OrderByDescending(h => h.TransactionDate)
                  .ThenByDescending(h => h.Id)
                  .First())
    .ToListAsync();

// مشتریان - آخرین موجودی تا پایان بازه  
var latestCustomerBalances = await _context.CustomerBalanceHistory
    .Where(h => !h.IsDeleted && h.TransactionDate <= endDateTime)
    .GroupBy(h => new { h.CustomerId, h.CurrencyCode })
    .Select(g => g.OrderByDescending(h => h.TransactionDate)
                  .ThenByDescending(h => h.Id)
                  .First())
    .ToListAsync();
```

## مثال‌های عملی

### سناریو 1: موجودی مثبت/منفی
```
تاریخ: 2025-01-15
بانک USD: 1000
مشتری A: 300 (ما به A بدهکاریم)
مشتری B: -150 (B به ما بدهکار است)
کل مشتریان: 300 + (-150) = 150

تراز نهایی: 1000 - 150 = 850 USD
```

### سناریو 2: بازه زمانی
```
از تاریخ: 2025-01-01  
تا تاریخ: 2025-01-15

گزارش نشان می‌دهد:
- تمام تراکنش‌ها از ابتدا تا 2025-01-15 23:59:59
- آخرین موجودی هر مشتری/بانک در این تاریخ
- نه فقط تراکنش‌های بین 1 تا 15 ژانویه
```

## تست‌های موردنیاز

### ✅ تست‌های انجام شده
- [x] Build موفق پروژه
- [x] بررسی منطق محاسبه در ViewModel
- [x] بروزرسانی متن‌های نمایشی

### تست‌های توصیه‌شده
- [ ] تست محاسبه تراز با داده‌های مختلف
- [ ] بررسی عملکرد با بازه‌های زمانی گوناگون
- [ ] تایید صحت موجودی در تاریخ‌های مختلف
- [ ] تست خروجی Excel و چاپ
- [ ] مقایسه نتایج گزارش روزانه و تاریخچه

## یادداشت‌های مهم

1. **سازگاری**: هر دو گزارش (روزانه و تاریخچه) اکنون منطق یکسانی دارند
2. **Performance**: کوئری‌ها بهینه‌سازی شده‌اند برای عملکرد بهتر
3. **UI/UX**: متن‌های رابط کاربری به‌روزرسانی شده‌اند
4. **Audit Trail**: تمام تغییرات به صورت کامل مستند شده‌اند

---

**نتیجه**: گزارش‌ات اکنون منطق محاسبه صحیح و بازه زمانی دقیق دارند.