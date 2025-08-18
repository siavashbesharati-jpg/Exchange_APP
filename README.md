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

#### 🚧 **در دست اجرا**
- 📸 **پردازش رسید**: تشخیص متن با OpenRouter API
- ✅ **تسویه تراکنش**: فرآیند کامل تأیید و تسویه
- 📈 **گزارش‌دهی**: سیستم گزارش‌گیری مالی
- 👤 **پروفایل مشتری**: داشبورد شخصی مشتری‌ها

### 🗂️ ساختار پروژه
```
ForexExchange/
├── Controllers/          # کنترلرها
│   ├── HomeController.cs
│   ├── OrdersController.cs
│   ├── CustomersController.cs
│   └── ExchangeRatesController.cs
├── Models/              # مدل‌های دیتا
│   ├── Customer.cs
│   ├── Order.cs
│   ├── Transaction.cs
│   ├── Receipt.cs
│   ├── ExchangeRate.cs
│   └── ForexDbContext.cs
├── Views/               # صفحات نمایش
│   ├── Home/
│   ├── Orders/
│   ├── Customers/
│   ├── ExchangeRates/
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

# ایجاد دیتابیس
dotnet ef database update

# اجرای برنامه
dotnet run
```

### 🌐 دسترسی
- **URL محلی**: http://localhost:5000
- **محیط توسعه**: HTTPS روی پورت 7000

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

### 🔒 امنیت
- Validation در سمت کلاینت و سرور
- پایگاه داده SQLite محلی
- مخفی‌سازی فایل‌های حساس در .gitignore

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
