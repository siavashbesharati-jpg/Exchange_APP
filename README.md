# 🏗️ سامانه صرافی ایران اکسپدیا
## Market Maker Exchange System - سیستم صرافی بازارساز

### 📋 Business Model - مدل کسب و کار

#### 🏦 **Market Maker Exchange (بازارساز)**
این سامانه به عنوان **بازارساز (Market Maker)** عمل می‌کند، نه صرفاً واسط:

**🔄 مدل عملیاتی:**
- صرافی با **سرمایه خود** خرید و فروش می‌کند
- هر مشتری با **خود صرافی** معامله می‌کند (نه با مشتریان دیگر)
- صرافی **ریسک موجودی** ارزهای مختلف را مدیریت می‌کند
- درآمد از **اسپرد** (تفاوت نرخ خرید و فروش) و **کمیسیون**

#### 💰 **Credit Pool System - سیستم استخر اعتباری**

**مثال عملیاتی:**
```
وضعیت اولیه: USD Pool = 0, Toman Pool = 0

مشتری A: خرید 1000 USD @ 98,000 تومان
→ USD Pool = -1000 (صرافی 1000 دلار بدهکار)
→ Toman Pool = +98,000,000 (صرافی 98 میلیون تومان دریافت کرد)

مشتری B: فروش 500 USD @ 97,000 تومان  
→ USD Pool = -500 (موجودی خالص: 500 دلار بدهکار)
→ Toman Pool = +49,500,000 (موجودی خالص: 49.5 میلیون تومان)

سود: (98,000 - 97,000) × 500 = 500,000 تومان اسپرد
```

#### 📊 **Real-time Pool Tracking - ردیابی لحظه‌ای استخر**
- **Pool مثبت**: صرافی از آن ارز اضافه دارد
- **Pool منفی**: صرافی آن ارز را بدهکار است
- **مدیریت ریسک**: نظارت لحظه‌ای بر موجودی هر ارز
- **گزارش‌گیری**: وضعیت استخر در داشبورد

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
- � **مدیریت نرخ ارز**: بروزرسانی زنده نرخ‌ها با web scraping
- 📝 **ثبت سفارش**: فرم جامع ثبت سفارش با محاسبه زنده
- � **موتور مچینگ هوشمند**: تطبیق سفارش‌ها با partial fills
- 👥 **مدیریت مشتری کامل**: احراز هویت با شماره تلفن
- 💾 **پایگاه داده**: SQLite با Entity Framework Core
- 🤖 **پردازش OCR**: تشخیص متن رسیدها با AI
- ✅ **سیستم تسویه**: فرآیند کامل تأیید و تسویه
- 📈 **گزارش‌گیری پیشرفته**: گزارشات مالی و pool tracking
- 👤 **پروفایل مشتری**: تاریخچه کامل معاملات
- 🏦 **پردازش بانکی**: تجزیه خودکار صورت حساب‌ها
- 📬 **سیستم اعلانات**: اطلاع‌رسانی خودکار
- 🔐 **کنترل دسترسی**: نقش‌های مختلف کاربری
- ⏰ **DateTime Standardization**: زمان محلی به جای UTC

#### 🚧 **در حال توسعه**
- � **Real-time Credit Pool Dashboard**: نمایش لحظه‌ای موجودی ارزها
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
- **نقش‌های کاربری**: Admin, Staff, Customer
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
- **ثبت سفارش**: فرم ثبت سفارش جدید
- **مدیریت سفارش‌ها**: لیست و مدیریت سفارش‌ها
- **مدیریت نرخ‌ها**: بروزرسانی نرخ‌های ارز
- **مدیریت مشتری‌ها**: ثبت و ویرایش مشتری‌ها
- **گزارش‌گیری**: گزارشات مالی و عملکرد
- **تسویه حساب**: مدیریت و پردازش تسویه‌ها
- **مدیریت رسیدها**: آپلود و پردازش رسیدهای بانکی
- **احراز هویت**: ورود، ثبت نام و مدیریت پروفایل

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
**تاریخ آخرین بروزرسانی**: ۲۸ مرداد ۱۴۰۳ (۱۸ اوت ۲۰۲۵)
