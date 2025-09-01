# سامانه معاملات  ایران اکسپدیا (IranExpedia Exchange)

Multi-Currency Cross-Trading Exchange System — سیستم معاملات  چندارزه با تجارت متقابل و مدیریت استخرهای ارزی

## فهرست مطالب (Table of Contents)
- معرفی و مدل کسب‌وکار
- ویژگی‌ها و فناوری‌ها
- نقش‌ها و دسترسی‌ها (Orders فقط برای Admin/Manager/Staff)
- معماری و موجودیت‌های اصلی
- گردش‌کار: از سفارش تا تسویه (Business Flow)
- استخرهای ارزی و مدیریت ریسک
- ساختار پروژه
- نصب، راه‌اندازی و پیکربندی
- امنیت و مجوزها
- گزارش‌ها، رسیدها (OCR) و ماژول‌ها
- راهنمای توسعه و گیت‌فلو
- نقشه‌راه و TODO
- تاریخچه ریفکتور (CurrencyType → Currency DB)
- پشتیبانی و اطلاعات نسخه
 - مستندات کامل (Wiki)

## معرفی و مدل کسب‌وکار
این سامانه یک معاملات  چندارزه با قابلیت معاملات متقابل است. هر ارز می‌تواند مستقیماً با هر ارز دیگری معامله شود (USD↔EUR, AED↔TRY, IRR↔OMR). نرخ‌ها بر اساس موجودی استخرها و ریسک تنظیم می‌شوند؛ درآمد از اسپرد و کمیسیون تأمین می‌گردد.

## ویژگی‌ها و فناوری‌ها
- Framework: ASP.NET Core MVC 9.0
- Database: SQLite + EF Core
- UI: Bootstrap 5 RTL (فارسی) + فونت Vazirmatn
- معماری: برنامه MVC یکپارچه

ویژگی‌های کلیدی:
- رابط فارسی راست‌چین، داشبورد نقش‌محور
- موتور مچینگ با پشتیبانی Partial Fill و چند‌تطبیقی
- استخرهای ارزی برای همه ارزها (شامل تومان به‌عنوان پایه)
- آپلود رسید و OCR مبتنی بر AI، مدیریت تسویه کامل
- گزارش‌های مالی، کارمزد/کمیسیون و فعالیت مشتری
- اعلان‌ها، احراز هویت و کنترل دسترسی مبتنی بر نقش

## نقش‌ها و دسترسی‌ها
- Admin, Manager, Staff: ایجاد/مدیریت سفارش‌ها، رسیدها، تسویه‌ها، گزارش‌ها؛ مدیریت ارزها و نرخ‌ها.
- Customer: مشاهده پروفایل و تراکنش‌های خود؛ ایجاد یا مدیریت سفارش مجاز نیست.

نکته: ورودی‌های UI برای ثبت سفارش فقط برای Admin/Manager/Staff نمایش داده می‌شوند.

## معماری و موجودیت‌های اصلی
- Currency (DB): Code, PersianName, Symbol, IsActive, IsBaseCurrency(=IRR), DisplayOrder
- Order: OrderType(Buy/Sell), FromCurrencyId, ToCurrencyId, Amount, Rate, Status, FilledAmount, CustomerId, TotalInToman
- Transaction: BuyOrderId, SellOrderId، Amount, Rate, Status, TotalInToman, timestamps
- Receipt: تصویر + فیلدهای OCR (مبلغ/مرجع/تاریخ)، IsVerified

نرخ‌گذاری در ایجاد سفارش: مستقیم (From→To)، معکوس (۱/To→From) یا Cross از طریق IRR وقتی دو ساقه فعال باشند.

## گردش‌کار: از سفارش تا تسویه (خلاصه)
1) آماده‌سازی سیستم: Seed و مدیریت ارزها؛ IRR تنها ارز پایه. تنظیمات کمیسیون/کارمزد و حدود عملیاتی.
2) ایجاد سفارش (Admin/Manager/Staff): انتخاب مشتری و جفت‌ارز، تعیین نرخ (مستقیم/معکوس/از طریق IRR). ذخیره با Status=Open.
3) مچینگ: انتخاب بهترین تطبیق‌ها با پشتیبانی Partial Fill؛ ساخت Transaction و به‌روزرسانی FilledAmount/Status؛ بروزرسانی استخرها.
4) تسویه: آپلود رسید (OCR اختیاری) → تأیید رسید → تأیید پرداخت‌ها → تکمیل یا Fail با Rollback.
5) کارمزد: از SettingsService خوانده می‌شود (درصد → نسبت اعشاری) و در Settlement لحاظ می‌گردد.
6) گزارش‌گیری: مالی/کمیسیون/فعالیت مشتری/OrderBook + خروجی CSV.

برای جزئیات و سناریوهای نمونه، بخش «Business Flow» در همین فایل پوشش داده شده است.

## استخرهای ارزی و مدیریت ریسک (خلاصه)
- برای هر ارز یک Pool نگهداری می‌شود؛ تراکنش‌ها باعث تغییر موجودی می‌گردند (خرید = پرداخت از استخر مقصد، فروش = دریافت در استخر مبدأ).
- ریسک‌ها: موجودی (بالانس ±)، نوسان، تمرکز و نقدینگی. هشدارها بر اساس آستانه‌های عملیاتی.

نمونه تراکنش زنجیره‌ای: افزایش/کاهش موجودی USD/EUR/AED و کسری IRR که نیازمند تعادل‌سازی است.

## ساختار پروژه
```
ForexExchange/
├── Controllers/ (Home, Orders, Customers, ExchangeRates, Account, Reports, Receipts, Settlements, BankStatements)
├── Models/ (Order, Transaction, Receipt, ExchangeRate, Customer, Currency, …)
├── Services/ (OCR, Settlement, Pool, Notification, BankStatement, Settings, …)
├── Views/ (Razor Pages per module)
├── wwwroot/ (Static)  └── Migrations/
```

## نصب، راه‌اندازی و پیکربندی
پیش‌نیازها: .NET 9 SDK، Visual Studio 2022 یا VS Code

راه‌اندازی (PowerShell):
```powershell
# کلون
git clone <repository-url>
cd Exchange_APP/ForexExchange

# وابستگی‌ها
dotnet restore

# دیتابیس
dotnet ef database update

# اجرا
dotnet run
```

ورود پیش‌فرض Admin:
- Email: admin@iranexpedia.com
- Password: Admin123!

تنظیم OCR (اختیاری): appsettings.Development.json → OpenRouter.ApiKey را مقداردهی کنید.

## امنیت و مجوزها
- ASP.NET Core Identity، نقش‌ها: Admin, Manager, Staff, Customer
- RBAC، Anti-forgery، ولیدیشن ورودی سمت کلاینت/سرور
- مخفی‌سازی مقادیر حساس در تنظیمات

## گزارش‌ها، رسیدها (OCR) و ماژول‌ها
- Reports: مالی، کمیسیون، OrderBook، فعالیت مشتری + CSV
- Receipts: آپلود تصویر، OCR (AI)، تأیید و پیوست به تراکنش/سفارش
- Settlements: PaymentUploaded → ReceiptConfirmed → Completed/Failed
- ExchangeRates: مدیریت نرخ‌های مستقیم/معکوس/از طریق IRR
- Currencies: مدیریت ارزها (بدون حذف؛ فعال/غیرفعال؛ IRR غیرقابل غیرفعال)

## راهنمای توسعه و گیت‌فلو (خلاصه)
- شاخه‌ها: master (Production), develop, feature/*, bugfix/*, hotfix/*, docs/*
- کامیت‌ها (Conventional): feat|fix|docs|refactor|test|chore(scope): message
- PRها: شرح تغییرات، نوع تغییر، وضعیت تست، اسکرین‌شات (در صورت UI)
- راه‌اندازی توسعه: clone → restore → build → ef update → run

## نقشه‌راه و TODO (خلاصه)
- Refactor تکمیلی: حذف کامل وابستگی‌های قدیمی و تکمیل Viewها/Controllers/Seed
- بهبود موتور مچینگ: FIFO برای نرخ‌های برابر، Smart Execution
- داشبورد Pool لحظه‌ای، نمودار تاریخچه، هشدارهای ریسک
- گزارش‌های پیشرفته سود/زیان و اسپرد
- امنیت پیشرفته، تست‌های واحد و لاگینگ ساختاریافته

## تاریخچه ریفکتور (CurrencyType → Currency DB)
- حذف Enum قدیمی و مهاجرت به موجودیت Currency در پایگاه داده.
- Orders/Transactions اکنون FromCurrency/ToCurrency دارند؛ نمایش بر اساس PersianName/Code از DB.
- Seed ارزها: IRR (پایه) + USD/EUR/AED/OMR/TRY؛ جلوگیری از چند پایه و حذف.
- کنترلرها/ویوها به تدریج به مدل جدید به‌روزرسانی شده‌اند؛ سفارش‌سازی UI طبق نقش‌ها.

## پشتیبانی و اطلاعات نسخه
- Issues را در مخزن ثبت کنید.
- آخرین بروزرسانی: ۳۱ مرداد ۱۴۰۴ (۲۲ اوت ۲۰۲۵)

یادداشت: این README محتوای فایل‌های پیشین مانند BUSINESS_FLOW.md، IMPLEMENTATION_SUMMARY.md، REFACTOR_SUMMARY.md، CROSS_CURRENCY_IMPLEMENTATION.md، GITHUB_INSTRUCTIONS.md و TODO.md را در قالبی یکپارچه و خلاصه ادغام می‌کند. فایل‌های مذکور برای آرشیو همچنان در مخزن باقی مانده‌اند.

## مستندات کامل (Wiki)
برای جزئیات بیشتر، صفحات ویکی پروژه در پوشه docs در دسترس‌اند:
- Business & Domain Flow: docs/business-flow.md
- Technical Architecture: docs/architecture.md
- Currencies & Rates: docs/currencies-and-rates.md
- Pools & Risk Management: docs/pools-and-risk.md
- Development Guide: docs/development.md
- Refactor History: docs/refactor-history.md
- Roadmap & TODO: docs/roadmap-todo.md
