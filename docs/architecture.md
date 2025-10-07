# Technical Architecture

ASP.NET Core MVC 9.0, EF Core (SQLite), Razor views, Bootstrap 5 RTL.

## Key Models
- Currency, Order, Transaction, Receipt, ExchangeRate, SystemSettings, CurrencyPool, Notification.

## Services
- SettingsService: system settings (commission, fees, limits).
- CurrencyPoolService: updates/reads pools; risk hints.
- TransactionSettlementService: lifecycle and monetary calculations.
- OpenRouterOcrService: OCR via OpenRouter.
- BankStatementService, NotificationService.

## Controllers & Views
- Orders, Transactions/Settlements, Receipts, Reports, ExchangeRates, Currencies, Customers, Home.

## Notable Behaviors
- Rate resolution: direct → reverse (1/rev) → cross via IRR.
- TotalInToman: used for reporting and settlement.
- Role gating: Orders/Currencies for Admin/Operator.

## Data & Migrations
- See Migrations folder for schema; IRR is base currency (single base enforced during seeding/management).
