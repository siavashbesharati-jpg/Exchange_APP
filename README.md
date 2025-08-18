# 🏗️ سامانه صرافی ایران اکسپدیا
## Forex Order Matching & Transaction Automation System

### 📋 توضیحات پروژه
سامانه جامع صرافی برای مدیریت سفارش‌های خرید و فروش ارز با قابلیت‌های پیشرفته مچینگ و پردازش خودکار تراکنش‌ها.

### 🛠️ تکنولوژی‌های استفاده شده
- **Framework:** ASP.NET Core MVC 9.0
- **Database:** SQLite with Entity Framework Core
- **UI:** Bootstrap 5 RTL (Persian/Farsi)
- **Font:** Vazirmatn (Persian Typography)
- **Architecture:** Monolithic MVC Application

### ✨ ویژگی‌های اصلی

#### ✅ **اجرا شده**
- 🎯 **رابط کاربری فارسی**: کاملاً راست‌چین با Bootstrap 5 RTL
- 📊 **داشبورد مدیریت**: نمایش آمار و فعالیت‌های اخیر
- 🔄 **مدیریت نرخ ارز**: بروزرسانی زنده نرخ‌های ارز (دلار، یورو، درهم، ریال عمان، لیر ترکیه)
- 📝 **ثبت سفارش**: فرم جامع ثبت سفارش با محاسبه زنده
- 🔀 **موتور مچینگ**: مچ کردن خودکار سفارش‌های خرید و فروش
- 👥 **مدیریت مشتری**: ثبت و مدیریت اطلاعات مشتری‌ها
- 💾 **پایگاه داده**: SQLite با رابط Entity Framework
- 🤖 **پردازش OCR**: تشخیص متن رسیدها با OpenRouter API و Google Gemini 2.0 Flash
- ✅ **سیستم تسویه**: فرآیند کامل تأیید و تسویه تراکنش‌ها
- 📈 **گزارش‌گیری پیشرفته**: گزارشات مالی، فعالیت مشتریان و آمار تراکنش‌ها
- 👤 **پروفایل مشتری**: صفحه شخصی مشتری‌ها با تاریخچه تراکنش‌ها
- 🏦 **پردازش صورت حساب**: تجزیه خودکار صورت حساب‌های بانکی با AI
- 📬 **سیستم اعلانات**: اطلاع‌رسانی خودکار به مشتریان
- 🔐 **سیستم احراز هویت**: کاربران، نقش‌ها و کنترل دسترسی

#### 🎯 **آماده برای توسعه**
- 🔒 **سخت‌سازی امنیتی**: اعمال نکات امنیتی پیشرفته
- 👨‍💼 **پنل مدیریت**: پنل جامع مدیریت کاربران و سیستم
- 📧 **تأیید ایمیل**: سیستم تأیید ایمیل و بازیابی رمز عبور
- 🔍 **حسابرسی**: ثبت تمام فعالیت‌های کاربران

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
