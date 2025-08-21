# 🏗️ سامانه صرافی ایران اکسپدیا
## Multi-Currency Cross-Trading Exchange System - سیستم صرافی چندارزه با تجارت متقابل

### 📋 Business Model - مدل کسب و کار

#### 🔄 **Multi-Currency Cross-Trading Exchange (صرافی چندارزه با تجارت متقابل)**
این سامانه به عنوان **صرافی چندارزه** عمل می‌کند که امکان معاملات متقابل بین تمام ارزها را فراهم می‌کند:

**مدل عملیاتی:**
- **تجارت متقابل**: هر ارز می‌تواند مستقیماً با هر ارز دیگری معامله شود (USD↔EUR, AED↔TRY, Toman↔OMR)
- **استخر چندارزه**: تمام ارزها شامل تومان در استخرهای جداگانه مدیریت می‌شوند
- **ارزیابی ریسک هوشمند**: موتور ریسک برای ارزیابی و مدیریت موجودی تمام ارزها
- **نرخ‌گذاری پویا**: نرخ‌های متقابل بر اساس موجودی استخر و ریسک تنظیم می‌شوند
- درآمد از **اسپرد** و **کمیسیون** در تمام جفت ارزها

#### 💰 **Multi-Currency Pool System - سیستم استخر چندارزه**

**مثال عملیاتی جدید:**
```
وضعیت اولیه: All Pools = 0

Transaction 1: مشتری A - خرید 1000 USD با 98,000 Toman
→ USD Pool = +1000 (صرافی 1000 دلار دریافت کرد)
→ Toman Pool = -98,000,000 (صرافی 98 میلیون تومان پرداخت کرد)

Transaction 2: مشتری B - خرید 800 EUR با 500 USD
→ EUR Pool = +800 (صرافی 800 یورو دریافت کرد)
→ USD Pool = +1000-500 = +500 (صرافی 500 دلار پرداخت کرد)

Transaction 3: مشتری C - خرید 2000 AED با 400 EUR
→ AED Pool = +2000 (صرافی 2000 درهم دریافت کرد)
→ EUR Pool = +800-400 = +400 (صرافی 400 یورو پرداخت کرد)

موجودی نهایی:
- USD Pool: +500 (مازاد)
- EUR Pool: +400 (مازاد)  
- AED Pool: +2000 (مازاد)
- Toman Pool: -98,000,000 (کسری)

ریسک: تومان در موقعیت منفی قابل توجه - نیاز به تعادل
```

#### 📊 **Advanced Risk Management - مدیریت ریسک پیشرفته**
- **ریسک موجودی**: نظارت بر موجودی مثبت/منفی هر ارز
- **ریسک نوسانات**: ارزیابی تغییرات نرخ ارز بین جفت‌های مختلف
- **ریسک تمرکز**: جلوگیری از تمرکز بیش از حد در یک ارز
- **ریسک نقدینگی**: اطمینان از قابلیت تأمین تقاضا برای هر ارز
- **هشدارهای هوشمند**: اعلان‌های خودکار برای وضعیت‌های پرریسک

### 🛠️ تکنولوژی‌های استفاده شده
- **Framework:** ASP.NET Core MVC 9.0
- **Database:** SQLite with Entity Framework Core
- **UI:** Bootstrap 5 RTL (Persian/Farsi)
- **Font:** Vazirmatn (Persian Typography)
- **Architecture:** Monolithic MVC Application

### ✨ ویژگی‌های اصلی

#### ✅ **پیاده‌سازی شده**
- 🎯 **رابط کاربری فارسی**: کاملاً راست‌چین با Bootstrap 5 RTL
- 📊 **داشبورد نقش‌محور**: داشبورد متفاوت برای عموم، مشتری و مدیر
- 🏦 **سیستم Market Maker**: صرافی به عنوان بازارساز عمل می‌کند
- 💱 **مدیریت نرخ ارز**: بروزرسانی زنده نرخ‌ها با web scraping
- 📝 **ثبت سفارش (فقط Admin/Manager/Staff)**: فرم جامع ثبت سفارش با محاسبه زنده
- 🤝 **موتور مچینگ هوشمند**: تطبیق سفارش‌ها با partial fills
- 👥 **مدیریت مشتری کامل**: احراز هویت با شماره تلفن
- 💾 **پایگاه داده**: SQLite با Entity Framework Core
- 🤖 **پردازش OCR**: تشخیص متن رسیدها با AI
- ✅ **سیستم تسویه**: فرآیند کامل تأیید و تسویه
- 📈 **گزارش‌گیری پیشرفته**: گزارشات مالی و pool tracking
- 👤 **پروفایل مشتری**: تاریخچه کامل معاملات
- 🏦 **پردازش بانکی**: تجزیه خودکار صورت حساب‌ها
- 📬 **سیستم اعلانات**: اطلاع‌رسانی خودکار
- 🔐 **کنترل دسترسی**: نقش‌های مختلف کاربری (Order فقط برای Admin/Manager/Staff)
- ⏰ **DateTime Standardization**: زمان محلی به جای UTC

#### 🚧 **در حال توسعه**
- 📊 **Real-time Credit/Pools Dashboard**: نمایش لحظه‌ای موجودی ارزها
- 🎯 **Enhanced Matching Engine**: تطبیق بهتر با partial fills
- 📈 **Advanced Risk Management**: مدیریت ریسک پیشرفته
- 💹 **Pool Position Reports**: گزارشات موقعیت استخر اعتباری
- 🔔 **Pool Alert System**: هشدار برای موجودی‌های بحرانی

#### 🎯 **برنامه‌ریزی شده**
- 🤖 **Automated Pool Rebalancing**: تعادل خودکار استخرها
- 📱 **Mobile App**: اپلیکیشن موبایل
- 🔒 **Advanced Security**: امنیت پیشرفته
- 👨‍💼 **Admin Panel Enhancement**: پنل مدیریت کامل
- 📧 **Email Verification**: تأیید ایمیل کاربران
- 🔍 **Audit System**: سیستم حسابرسی

### 🗂️ ساختار پروژه
```
ForexExchange/
├── Controllers/          # کنترلرها
│   ├── HomeController.cs
│   ├── OrdersController.cs
│   ├── CustomersController.cs
│   ├── ExchangeRatesController.cs
│   ├── AccountController.cs          # احراز هویت
│   ├── ReportsController.cs          # گزارش‌گیری
│   ├── ReceiptsController.cs         # مدیریت رسیدها
│   ├── SettlementsController.cs      # تسویه حساب
│   └── BankStatementsController.cs   # صورت حساب بانکی
├── Models/              # مدل‌های دیتا
│   ├── Customer.cs
│   ├── Order.cs
│   ├── Transaction.cs
│   ├── Receipt.cs
│   ├── ExchangeRate.cs
│   ├── ApplicationUser.cs           # کاربران سیستم
│   ├── Notification.cs              # اعلانات
│   ├── AccountViewModels.cs         # مدل‌های احراز هویت
│   └── ForexDbContext.cs
├── Services/            # سرویس‌ها
│   ├── IOcrService.cs              # واسط پردازش OCR
│   ├── OpenRouterOcrService.cs     # سرویس OpenRouter AI
│   ├── ITransactionSettlementService.cs
│   ├── TransactionSettlementService.cs
│   ├── IEmailService.cs
│   ├── INotificationService.cs
│   ├── NotificationService.cs
│   ├── IBankStatementService.cs
│   ├── BankStatementService.cs
│   ├── IDataSeedService.cs
│   └── DataSeedService.cs          # اولیه‌سازی داده‌ها
├── Views/               # صفحات نمایش
│   ├── Home/
│   ├── Orders/
│   ├── Customers/
│   ├── ExchangeRates/
│   ├── Account/                    # صفحات احراز هویت
│   ├── Reports/                    # صفحات گزارش‌گیری
│   ├── Receipts/                   # مدیریت رسیدها
│   ├── Settlements/                # تسویه حساب
│   ├── BankStatements/             # صورت حساب بانکی
│   └── Shared/
├── wwwroot/            # فایل‌های استاتیک
└── Migrations/         # مایگریشن‌های دیتابیس
```

### 🔧 نصب و راه‌اندازی

#### پیش‌نیازها
- .NET 9.0 SDK
- Visual Studio 2022 یا VS Code

#### راه‌اندازی
```bash
# کلون کردن ریپازیتوری
git clone <repository-url>
cd Exchange_APP/ForexExchange

# بازسازی پکیج‌ها
dotnet restore

# ایجاد دیتابیس و اجرای مایگریشن‌ها
dotnet ef database update

# اجرای برنامه
dotnet run
```

#### 🔑 اطلاعات ورود پیش‌فرض
**مدیر سیستم:**
- **ایمیل**: `admin@iranexpedia.com`
- **رمز عبور**: `Admin123!`
- **نقش**: Admin

### 🌐 دسترسی
- **URL محلی**: http://localhost:5063
- **محیط توسعه**: HTTPS در صورت تنظیم

### 🔐 احراز هویت و مجوزها
- **سیستم احراز هویت**: ASP.NET Core Identity
- **نقش‌های کاربری**: Admin, Manager, Staff, Customer
- **کنترل دسترسی**: Role-based authorization
- **رمزگذاری**: BCrypt password hashing
- **محافظت از صفحات**: Authorization attributes

### 💱 ارزهای پشتیبانی شده
- 🇺🇸 **دلار آمریکا (USD)**
- 🇪🇺 **یورو (EUR)**  
- 🇦🇪 **درهم امارات (AED)**
- 🇴🇲 **ریال عمان (OMR)**
- 🇹🇷 **لیر ترکیه (TRY)**
- 🇮🇷 **تومان (پایه محاسبات)**

### 📱 صفحات اصلی
- **داشبورد اصلی**: نمای کلی سیستم و آمار
- **ثبت سفارش (Admin/Manager/Staff)**: فرم ثبت سفارش جدید
- **مدیریت سفارش‌ها (Admin/Manager/Staff)**: لیست و مدیریت سفارش‌ها
- **مدیریت نرخ‌ها**: بروزرسانی نرخ‌های ارز
- **مدیریت مشتری‌ها**: ثبت و ویرایش مشتری‌ها
- **گزارش‌گیری**: گزارشات مالی و عملکرد
- **تسویه حساب**: مدیریت و پردازش تسویه‌ها
- **مدیریت رسیدها**: آپلود و پردازش رسیدهای بانکی
- **احراز هویت**: ورود، ثبت نام و مدیریت پروفایل

### 📘 گردش‌کار کسب‌وکار (Business Flow)
برای جزئیات کامل گردش‌کار از «ثبت سفارش» تا «تسویه»، همراه با سناریوهای نمونه، فایل زیر را ببینید:
- BUSINESS_FLOW.md

### 🔒 امنیت
- Authentication و Authorization با ASP.NET Core Identity
- Password hashing و security policies
- Role-based access control
- Anti-forgery token protection
- Input validation در client و server
- Database security با Entity Framework
- مخفی‌سازی اطلاعات حساس در configuration

### 📝 مستندات API
تمام کنترلرها شامل متدهای RESTful برای:
- GET: نمایش اطلاعات
- POST: ایجاد رکورد جدید  
- PUT: بروزرسانی اطلاعات
- DELETE: حذف (soft delete)

### 🚀 نسخه‌های آینده
- 🔗 **API Integration**: اتصال به OpenRouter برای OCR
- 📊 **Advanced Reporting**: گزارش‌های تحلیلی پیشرفته
- 🔔 **Real-time Notifications**: اعلان‌های لحظه‌ای
- 📱 **Mobile Responsive**: بهینه‌سازی برای موبایل
- 🌐 **Multi-language**: پشتیبانی از زبان‌های متعدد

### 👨‍💻 توسعه‌دهنده
**GitHub Copilot** - سیستم هوش مصنوعی توسعه نرم‌افزار

### 📞 پشتیبانی
برای گزارش مشکلات یا پیشنهادات، لطفاً یک Issue ایجاد کنید.

---
**تاریخ آخرین بروزرسانی**: ۳۱ مرداد ۱۴۰۴ (۲۲ اوت ۲۰۲۵)
