# CurrencyType Enum to Database-Driven Currency System Refactor

## Overview
Complete removal of CurrencyType enum and transition to database-driven Currency entity system for cross-currency trading support.

## Current Status
- **Completed:** 95% of core Models and Services layer conversion ✅
- **Remaining:** ~280 compilation errors across the entire application ❌

## Summary of Remaining Work by File Type

### 1. **Views (.cshtml files) - ~150 errors**

**Problem:** Views still reference the old `CurrencyType` enum and removed `.Currency` property

**Files affected:**
- `Views/Orders/*.cshtml` (Create, Edit, Details, Index)
- `Views/Reports/*.cshtml` (Financial, OrderBook)
- `Views/ExchangeRates/*.cshtml` (Index, Manage)
- `Views/Home/Index.cshtml`
- `Views/Shared/_PoolWidget.cshtml`
- `Views/CustomerTransactions/*.cshtml`
- `Views/Receipts/*.cshtml`
- `Views/Settlements/*.cshtml`
- `Views/Customers/*.cshtml`
- `Views/Settings/Index.cshtml`

**Required Changes:**

1. **Currency Dropdown Menus:**
```csharp
// OLD - Hardcoded enum values
<option value="1" selected="@(Model.FromCurrency == ForexExchange.Models.CurrencyType.USD)">USD</option>

// NEW - Dynamic from database
@foreach(var currency in ViewBag.Currencies as List<Currency>)
{
    <option value="@currency.Id" selected="@(Model.FromCurrencyId == currency.Id)">@currency.Name (@currency.Code)</option>
}
```

2. **Currency Display Logic:**
```csharp
// OLD - Enum-based switch statements
@switch(transaction.Currency)
{
    case ForexExchange.Models.CurrencyType.USD: <text>USD</text> break;
}

// NEW - Database entity properties
@transaction.FromCurrency?.Code - @transaction.ToCurrency?.Code
```

3. **Helper Methods:**
```csharp
// OLD - CurrencyType parameter
string GetCurrencyName(ForexExchange.Models.CurrencyType currency)

// NEW - String parameter with database lookup
string GetCurrencyName(string currencyCode)
```

4. **Model Property Access:**
```csharp
// OLD - Single currency property
@order.Currency

// NEW - Cross-currency properties
@order.FromCurrency?.Code/@order.ToCurrency?.Code
```

### 2. **Controllers - ~50 errors**

**Problem:** Controllers still access removed `.Currency` property and use enum logic

**Files affected:**
- `Controllers/OrdersController.cs`
- `Controllers/ExchangeRatesController.cs` (remaining methods)
- `Controllers/HomeController.cs`
- `Controllers/BankStatementsController.cs`

**Required Changes:**

1. **Property Access Updates:**
```csharp
// OLD
if (order.Currency == CurrencyType.USD)

// NEW
if (order.FromCurrency?.Code == "USD")
```

2. **Query Logic:**
```csharp
// OLD
.Where(o => o.Currency == currency)

// NEW
.Where(o => o.FromCurrencyId == currencyId || o.ToCurrencyId == currencyId)
```

3. **Model Creation:**
```csharp
// OLD
new Transaction { Currency = CurrencyType.USD }

// NEW
new Transaction { FromCurrencyId = usdCurrencyId, ToCurrencyId = irrCurrencyId }
```

4. **ViewBag Data:**
```csharp
// OLD
ViewBag.Currencies = Enum.GetValues<CurrencyType>()

// NEW
ViewBag.Currencies = await _context.Currencies.Where(c => c.IsActive).ToListAsync()
```

### 3. **Services - ~20 errors**

**Problem:** Services still reference removed properties and enum types

**Files affected:**
- `Services/NotificationService.cs`
- `Services/TransactionSettlementService.cs`
- `Services/DataSeedService.cs`

**Required Changes:**

1. **Notification Messages:**
```csharp
// OLD
$"Order for {order.Currency} has been filled"

// NEW
$"Order for {order.FromCurrency?.Code}/{order.ToCurrency?.Code} has been filled"
```

2. **Settlement Logic:**
```csharp
// OLD
if (buyOrder.Currency == sellOrder.Currency)

// NEW
if (buyOrder.FromCurrencyId == sellOrder.ToCurrencyId && 
    buyOrder.ToCurrencyId == sellOrder.FromCurrencyId)
```

### 4. **Data Seeding (DataSeedService.cs) - ~50 errors**

**Problem:** Database initialization still uses hardcoded enum values

**Required Changes:**

1. **Currency Entity Creation:**
```csharp
// OLD
new ExchangeRate { Currency = CurrencyType.USD, BuyRate = 42000, SellRate = 42500 }

// NEW
var usdCurrency = await _context.Currencies.FirstAsync(c => c.Code == "USD");
var irrCurrency = await _context.Currencies.FirstAsync(c => c.IsBaseCurrency);
new ExchangeRate { FromCurrencyId = usdCurrency.Id, ToCurrencyId = irrCurrency.Id, BuyRate = 42000, SellRate = 42500 }
```

2. **Order/Transaction Seeding:**
```csharp
// OLD
new Order { Currency = CurrencyType.EUR }

// NEW
new Order { FromCurrencyId = eurId, ToCurrencyId = irrId }
```

3. **Replace Enum Arrays:**
```csharp
// OLD
var currencies = new[] { CurrencyType.USD, CurrencyType.EUR, CurrencyType.AED };

// NEW
var currencies = await _context.Currencies.Where(c => c.IsActive && !c.IsBaseCurrency).ToListAsync();
```

### 5. **Database Context (ForexDbContext.cs) - ~30 errors**

**Problem:** Entity configuration and model builder still reference enum

**Required Changes:**

1. **Remove Enum Conversions:**
```csharp
// OLD
builder.Property(e => e.Currency).HasConversion<int>();

// NEW
// Remove - now using foreign key relationships
```

2. **Update Seed Data:**
```csharp
// OLD
modelBuilder.Entity<ExchangeRate>().HasData(
    new ExchangeRate { Id = 1, Currency = CurrencyType.USD, BuyRate = 42000 }
);

// NEW
// Move to DataSeedService with database lookups
```

### 6. **ViewModels/DTOs - ~10 errors**

**Problem:** ViewModels still use CurrencyType properties

**Files affected:**
- `Models/AccountViewModels.cs`
- Various ViewModel classes in controllers

**Required Changes:**

1. **Property Type Updates:**
```csharp
// OLD
public CurrencyType DefaultCurrency { get; set; }

// NEW
public int DefaultCurrencyId { get; set; }
public Currency DefaultCurrency { get; set; }
```

## **Recommended Fix Order:**

1. **Start with DataSeedService** - Fix database initialization
2. **Update Controllers** - Fix backend logic
3. **Fix Core Services** - Notification and settlement logic
4. **Update Views systematically** - Start with most critical user-facing pages
5. **Clean up Database Context** - Remove old configurations

## **Estimated Effort:**
- **DataSeedService:** 2-3 hours (complex database initialization)
- **Controllers:** 2-4 hours (query logic updates)
- **Views:** 4-6 hours (many files, UI logic)
- **Services:** 1-2 hours (straightforward property updates)
- **Database Context:** 1 hour (cleanup)

**Total: 10-16 hours** of systematic refactoring to complete the enum-to-database conversion.

## **Priority Files:**
1. `Services/DataSeedService.cs` - Critical for application startup
2. `Controllers/OrdersController.cs` - Core business functionality
3. `Views/Orders/Create.cshtml` - User-facing order creation
4. `Views/Orders/Edit.cshtml` - User-facing order editing
5. `Controllers/ExchangeRatesController.cs` - Remaining methods
6. `Models/ForexDbContext.cs` - Database configuration cleanup

## **Completion Criteria:**
- [ ] Zero compilation errors
- [ ] All CurrencyType references removed
- [ ] All views use database-driven currency dropdowns
- [ ] All controllers use Currency entity relationships
- [ ] Data seeding works with database currencies
- [ ] Application builds and runs successfully
- [ ] Cross-currency functionality works end-to-end

---
**Date Created:** August 21, 2025
**Status:** In Progress - 95% Core Infrastructure Complete
**Next Step:** DataSeedService refactoring
