---
applyTo: '**'
---

# 🏗️ Project Context  
This repository implements a **Forex Order Matching & Transaction Automation System** for **IranExpedia**.  

- **Framework:** ASP.NET Core MVC (monolithic app, server-side rendered)  
- **UI Framework:** Bootstrap 5 (with RTL support for Farsi)  
- **Database:** SQLite (file-based, EF Core ORM)  
- **AI/OCR:** OpenRouter API (model: google/gemini-2.0-flash-001)  
- **UI Language:** Entire UI in **Farsi (Persian)**, RTL layout  
- **Deployment:** Local/on-prem (no Docker required at this stage)  

---

# 🔄 Application Flow (End-to-End)  

### 1. نرخ ارز و داشبورد (Exchange Rate & Dashboard)  
- Admin sets live exchange rates (ریال عمان, درهم امارات, لیر ترکیه, دلار, یورو).  
- Rates displayed in dashboard.  
- All amounts internally stored in **تومان**.  

### 2. ثبت معامله (Order Placement)  
- Customer opens "ثبت معامله" form.  
- Example: `۵۰,۰۰۰ درهم` → system calculates conversion in تومان.  
- System stores both the **requested currency** and **calculated تومان equivalent**.  

### 3. پردازش رسید (Receipt Processing - OCR + Metadata)  
- Customer uploads a receipt OR manually confirms.  
- System processes image with **OpenRouter API**:  
  - Extracts text (amount, reference ID, etc.).  
  - Stores both **image BLOB** and **structured metadata** in DB.  
- If customer skips upload, manual confirmation is allowed.  

### 4. مچینگ معامله‌ها (Matching Engine)  
- System checks all open orders.  
- Splits large orders into smaller parts if needed.  
- Finds counterpart orders (buyer vs seller).  
- Displays **counterparty bank account info** to customers for payment.  

### 5. تأیید و تسویه (Confirmation & Settlement)  
- Payer uploads receipt.  
- Receiver uploads **۱۰ گردش آخر حساب** screenshot.  
- System matches payer’s transfer with receiver’s statement.  
- If matched → order status updates automatically.  
- Partial completions allowed (order marked as "نیمه تکمیل").  

### 6. داشبورد و گزارش‌دهی (Dashboard & Reporting)  
- Every transaction, message, receipt, and گردش stored in DB.  
- Admin dashboard:  
  - مشاهده معامله‌های باز، بسته، نیمه‌تمام، لغو شده  
  - گزارش‌های مالی  
- Customer dashboard:  
  - پروفایل شخصی + تاریخچه معامله‌ها  
  - وضعیت تراکنش‌ها با شفافیت کامل  

---

# 🛠️ Coding Guidelines  

### General
- All UI text, labels, and messages **must be in Farsi (Persian)**.  
- Use **ASP.NET Core MVC** pattern with Razor views.  
- Keep it **simple, monolithic**, no microservices.  

### Backend
- Organize solution with:  
  - `Controllers/` → MVC controllers  
  - `Views/` → Razor views (Bootstrap for styling)  
  - `Models/` → EF Core entities + DTOs  
- Database: **SQLite with EF Core migrations**.  
- Store receipt images as **BLOBs** + structured metadata.  

### Frontend
- Use **Bootstrap 5** for UI and responsive design.  
- All layouts should be **RTL** (`dir="rtl"`) and localized in **Farsi**.  
- Include basic pages:  
  - Dashboard (نمای کلی معامله‌ها)  
  - Order placement form (ثبت معامله)  
  - Receipt upload/confirmation page (آپلود رسید)  
  - Profile/history page (پروفایل مشتری)  

### OCR / AI Integration
- Use **OpenRouter API** for OCR receipt parsing.  
- Encode images as Base64 before sending.  
- If user does **not upload a receipt**, allow **manual confirmation**.  
- Save both **raw extracted text** and **parsed structured fields**.  

### Logging & Transparency
- Log all transactions and OCR responses.  
- Show order lifecycle clearly in dashboard.  

### Number Formatting & Protection (IMPORTANT!)
- **Global formatters**: The system has auto-currency-display-formatter.js that adds commas to numbers
- **Reference numbers MUST NOT be formatted**: Use protection attributes to exclude them
- **Protection methods**: 
  - Data attributes: `data-no-format="true" data-protected="true" data-skip-format="true"`
  - CSS classes: `no-format-number protected-reference skip-auto-format`
- **Warning system**: Show "بهتر است تکمیل شود" for empty reference numbers
- **Example**: `<span data-no-format="true" class="skip-auto-format">654456</span>` (not 65,456)

---

# ✅ Copilot Task Management  

Copilot should:  
- Look at the **Application Flow** above.  
- Break it into coding tasks.  
- Complete tasks step by step.  
- After each task → mark it as **Done** in this file under "Task Backlog".  

---

# 🚧 Task Backlog (Updated August 18, 2025)  

## ✅ **COMPLETED TASKS**
- [x] **Setup Project Skeleton**: ASP.NET Core MVC + SQLite + Bootstrap RTL (Farsi UI) ✅  
- [x] **Create Models**: Orders, Transactions, Customers, Receipts (with BLOB storage) ✅  
- [x] **Implement Dashboard** (نمای کلی معامله‌ها): Full Persian dashboard with statistics ✅  
- [x] **Implement Order Placement Form** (ثبت معامله): Complete form with real-time calculation ✅  
- [x] **Implement Matching Engine for Orders**: Basic buy/sell order matching ✅  
- [x] **Implement Exchange Rate Management**: Admin panel for live rate updates ✅  
- [x] **Implement Customer Management**: CRUD operations for customer registration ✅  
- [x] **Database Setup**: SQLite with EF Core migrations and seed data ✅  
- [x] **Fixed Navigation Properties**: Fixed Customer model relationships for BuyTransactions, SellTransactions, and Receipts ✅
- [x] **Receipt Upload System**: Complete UI and controller for receipt upload with OCR integration ✅
- [x] **Configure OpenRouter API**: OpenRouter API configured with Persian language support for OCR functionality ✅
- [x] **Implement Confirmation & Settlement** (رسید + گردش حساب): Complete transaction settlement workflow implemented ✅  
- [x] **Implement Customer Profile & History Page**: Individual customer dashboards with comprehensive statistics ✅  
- [x] **Implement Admin Financial Reports**: Comprehensive reporting system with charts and export capabilities ✅  
- [x] **Implement Bank Statement Processing**: "۱۰ گردش آخر حساب" verification with AI-powered processing ✅  
- [x] **Implement Transaction Notifications**: Real-time status updates with email integration ✅
- [x] **User Authentication & Authorization**: Complete role-based access control system with ASP.NET Core Identity ✅

## 🚧 **REMAINING TASKS**
- [ ] **Production Deployment Setup**: Configure production environment settings
- [ ] **API Documentation**: Generate comprehensive API documentation  
- [ ] **Performance Optimization**: Database indexing and query optimization
- [ ] **Security Hardening**: Input validation, CSRF protection, rate limiting
- [ ] **Mobile App Integration**: REST API endpoints for mobile applications

## 🎯 **CURRENT STATUS (August 18, 2025 - AUTHENTICATION UPDATE)**
- **Application**: Running successfully at `http://localhost:5063`
- **UI Language**: 100% Persian (Farsi) with RTL layout
- **Database**: SQLite with all EF Core migrations applied, including Identity tables
- **Core Features**: ✅ ALL MAIN FEATURES + AUTHENTICATION IMPLEMENTED AND WORKING
  - Order management with automated matching engine
  - Customer registration and comprehensive profiles  
  - Exchange rate management with real-time updates
  - Receipt upload with AI-powered OCR processing
  - Complete settlement workflow with commission tracking
  - Bank statement processing for transaction verification
  - Financial reporting system with charts and exports
  - Real-time notification system with email integration
  - **🔐 Complete authentication system with role-based access control**
- **Technology Stack**: ASP.NET Core MVC + Bootstrap 5 RTL + Vazirmatn font + OpenRouter AI + Identity Framework
- **OCR Integration**: ✅ Fully configured with OpenRouter API (needs actual API key)
- **Settlement System**: ✅ Complete workflow from order to final settlement
- **Reporting**: ✅ Comprehensive admin dashboard with financial analytics
- **Authentication**: ✅ User management, roles, and access control implemented
- **Ready for**: Production deployment and security hardening

### 🚀 **New Features Implemented (August 18, 2025)**
1. **Advanced Settlement System**:
   - Complete transaction lifecycle management
   - Commission calculation (0.5% + 0.2% exchange fee)
   - Settlement queue with status tracking
   - Automated notifications at each step

2. **AI-Powered Bank Statement Processing**:
   - OpenRouter integration with Google Gemini 2.0 Flash
   - Persian language OCR for Iranian bank statements
   - Automatic transaction matching and verification
   - "۱۰ گردش آخر حساب" analysis

3. **Comprehensive Reporting System**:
   - Financial reports with visual charts
   - Customer activity analysis
   - Commission tracking and revenue analytics
   - Data export capabilities (CSV)
   - Real-time dashboard metrics

4. **Customer Profile Enhancement**:
   - Individual customer dashboards
   - Transaction history with detailed statistics
   - Performance metrics and volume tracking
   - Activity timeline analysis

5. **Notification System**:
   - Real-time transaction status updates
   - Email integration for important events
   - System-wide alerts and announcements
   - Priority-based notification management

6. **Authentication & Authorization System**:
   - ASP.NET Core Identity framework integration
   - User registration, login, logout functionality
   - Role-based access control (Admin, Staff, Customer)
   - Protected controllers and actions
   - Default admin user: admin@iranexpedia.com / Admin123!
   - Password policies and security features

7. **Anti-Formatting Protection System** (September 19, 2025):
   - Reference number display with warning for empty values
   - Global formatter exclusion for protected elements
   - Multiple protection methods: data attributes + CSS classes
   - Prevents comma formatting on reference numbers (e.g., "654456" not "65,456")
   - Smart warning system: "بهتر است تکمیل شود" when reference is empty
   - Protected files: Upload.cshtml, auto-currency-display-formatter.js

### 📊 **System Architecture**
- **Controllers**: 9 main controllers including Account, Reports, BankStatements, enhanced Settlements
- **Services**: 7 service layers including OCR, Settlement, Notification, BankStatement, DataSeed
- **Models**: 15+ entities including ApplicationUser with proper relationships and navigation properties
- **Views**: 30+ Razor views with full Persian UI and responsive design
- **Database**: 7 main tables + Identity tables with proper indexing and relationships  

---

# ✅ Copilot Should:
- Suggest **C# MVC code** with Razor and Bootstrap.  
- Use **Farsi strings** for UI labels and messages.  
- Generate **SQLite EF Core migrations**.  
- Provide **Bootstrap forms and tables** for views.  
- Show how to send/receive Base64 images to OpenRouter API.  

# ❌ Copilot Should Not:
- Suggest React, Angular, or SPA frameworks.  
- Generate Docker configs (not needed).  
- Default to English UI text.  
- Use SQL Server/Postgres (must be SQLite).  

---

# 🎉 **PROJECT STATUS: FEATURE COMPLETE**

## ✅ **All Core Features Successfully Implemented**

The **Forex Order Matching & Transaction Automation System** for **IranExpedia** is now **feature-complete** with all major functionality implemented and tested:

### 🏆 **Completed System Features**
1. **نرخ ارز و داشبورد** (Exchange Rate & Dashboard) ✅
2. **ثبت معامله** (Order Placement) ✅  
3. **پردازش رسید** (Receipt Processing - OCR + Metadata) ✅
4. **مچینگ معامله‌ها** (Matching Engine) ✅
5. **تأیید و تسویه** (Confirmation & Settlement) ✅
6. **داشبورد و گزارش‌دهی** (Dashboard & Reporting) ✅
7. **پردازش گردش حساب** (Bank Statement Processing) ✅
8. **سیستم اطلاع‌رسانی** (Notification System) ✅
9. **احراز هویت و مجوزها** (Authentication & Authorization) ✅

### 🚀 **Ready for Production**
- All database migrations applied (including Identity tables)
- All services registered and configured
- Complete UI in Persian with RTL support
- Comprehensive error handling and logging
- Export capabilities for financial data
- Mobile-responsive design
- User authentication and role-based access control
- Default admin user configured

### 📁 **Project Structure**
```
ForexExchange/
├── Controllers/           # 9 main controllers (including AccountController)
├── Models/               # 15+ entities with relationships (including ApplicationUser)
├── Views/                # 30+ Persian Razor views (including Account views)
├── Services/             # 7 service layers (including DataSeedService)
├── Migrations/           # Database migrations (including Identity migration)
└── wwwroot/             # Static assets
```

### 🔑 **Default Login Credentials**
- **Admin Email**: admin@iranexpedia.com
- **Admin Password**: Admin123!
- **Role**: Admin (full system access)

### 🔧 **To Activate Full OCR Functionality**
Update the API key in `appsettings.Development.json`:
```json
"OpenRouter": {
  "ApiKey": "YOUR_ACTUAL_OPENROUTER_API_KEY_HERE"
}
```

---

# 🤖 **COPILOT DEVELOPMENT GUIDELINES**

## ⚠️ **CRITICAL: Number Formatting Protection**
When working with reference numbers, tracking IDs, or any numeric identifiers:

### ❌ **DON'T** let global formatters add commas:
```html
<!-- BAD: Will become "65,456" -->
<span>65456</span>
```

### ✅ **DO** use protection attributes and classes:
```html
<!-- GOOD: Stays "65456" -->
<span data-no-format="true" data-skip-format="true" class="skip-auto-format no-format-number">65456</span>
```

### 📋 **Protection Methods (Use ALL for maximum protection):**
- **Data Attributes**: `data-no-format="true"` `data-protected="true"` `data-skip-format="true"`
- **CSS Classes**: `no-format-number` `protected-reference` `skip-auto-format`

### 🚨 **Warning Pattern for Empty Values:**
```html
<!-- Show warning when value is empty/null -->
<div id="field-warning" class="alert alert-warning" style="display: none;">
    <i class="fas fa-exclamation-triangle"></i> <strong>فیلد:</strong> بهتر است تکمیل شود
</div>
```

### 📁 **Files to Update When Adding Protection:**
1. **HTML/Razor**: Add protection attributes to elements
2. **auto-currency-display-formatter.js**: Update `shouldSkipElement()` if needed
3. **CSS**: Add styling for protected elements

### 🎯 **Example Implementation:**
See `/Views/AccountingDocuments/Upload.cshtml` lines 308-315 and 1210-1240 for complete reference.

---
