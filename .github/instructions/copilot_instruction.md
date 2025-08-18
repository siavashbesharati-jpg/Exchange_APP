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

### 2. ثبت سفارش (Order Placement)  
- Customer opens "ثبت سفارش" form.  
- Example: `۵۰,۰۰۰ درهم` → system calculates conversion in تومان.  
- System stores both the **requested currency** and **calculated تومان equivalent**.  

### 3. پردازش رسید (Receipt Processing - OCR + Metadata)  
- Customer uploads a receipt OR manually confirms.  
- System processes image with **OpenRouter API**:  
  - Extracts text (amount, reference ID, etc.).  
  - Stores both **image BLOB** and **structured metadata** in DB.  
- If customer skips upload, manual confirmation is allowed.  

### 4. مچینگ سفارش‌ها (Matching Engine)  
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
  - مشاهده سفارش‌های باز، بسته، نیمه‌تمام، لغو شده  
  - گزارش‌های مالی  
- Customer dashboard:  
  - پروفایل شخصی + تاریخچه سفارش‌ها  
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
  - Dashboard (نمای کلی سفارش‌ها)  
  - Order placement form (ثبت سفارش)  
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
- [x] **Implement Dashboard** (نمای کلی سفارش‌ها): Full Persian dashboard with statistics ✅  
- [x] **Implement Order Placement Form** (ثبت سفارش): Complete form with real-time calculation ✅  
- [x] **Implement Matching Engine for Orders**: Basic buy/sell order matching ✅  
- [x] **Implement Exchange Rate Management**: Admin panel for live rate updates ✅  
- [x] **Implement Customer Management**: CRUD operations for customer registration ✅  
- [x] **Database Setup**: SQLite with EF Core migrations and seed data ✅  

## 🚧 **REMAINING TASKS**
- [ ] **Implement Receipt Upload + OCR Integration**: OpenRouter API integration for receipt processing  
- [ ] **Implement Confirmation & Settlement** (رسید + گردش حساب): Complete transaction settlement workflow  
- [ ] **Implement Customer Profile & History Page**: Individual customer dashboards  
- [ ] **Implement Admin Financial Reports**: Comprehensive reporting system  
- [ ] **Implement Bank Statement Processing**: "۱۰ گردش آخر حساب" verification  
- [ ] **Implement Transaction Notifications**: Real-time status updates

## 🎯 **CURRENT STATUS (August 18, 2025)**
- **Application**: Running at `http://localhost:5000`
- **UI Language**: 100% Persian (Farsi) with RTL layout
- **Database**: SQLite with EF Core migrations applied
- **Core Features**: Order management, customer registration, exchange rates working
- **Technology Stack**: ASP.NET Core MVC + Bootstrap 5 RTL + Vazirmatn font
- **Ready for**: Receipt upload and OCR integration (next major milestone)  

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
