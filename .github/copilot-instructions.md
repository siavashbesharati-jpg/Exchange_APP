---
mode: agent
---

# GitHub Copilot Instructions

Multi-Currency Cross-Trading Exchange System with advanced financial audit trails, coherent balance management, and Persian RTL interface.

## Task Completion Protocol

When user confirms a task is completed successfully, follow this workflow:

1. **Git Operations** (in Persian):
   - `git add .`
   - `git commit` with proper Farsi commit message including:
     - Task description in Persian
     - Current timestamp
     - Changes summary
   - `git push`

2. **Task Report Creation or update **:
   - Create or update  file: `task-reports.md`
   - Content in Farsi with current timestamp
   - Mark task status as completed (تکمیل شده)
   - Include detailed summary of work done

## Architecture Overview

**ASP.NET Core 9.0 MVC** with **SQLite + Entity Framework Core**, featuring comprehensive financial operations with complete event sourcing through history tables.

### Core Domain Models
- **Currency**: Multi-currency support (USD, EUR, AED, OMR, TRY, IRR as base)
- **Order**: Cross-currency trading with `FromCurrency`/`ToCurrency` (replaces legacy enum)  
- **AccountingDocument**: Bilateral financial movements (Customer↔System↔Bank)
- **CentralFinancialService**: **Critical** - all financial operations go through this service for coherent balance chains

### Financial Architecture Pattern

**COHERENT BALANCE HISTORY** - The system maintains mathematically consistent balance chains:
```cs
// Pattern used throughout CentralFinancialService
balanceAfter = balanceBefore + transactionAmount
// ALL balance history records follow: IsCalculationValid() == true
```

**IsFrozen Strategy**: Records marked `IsFrozen=true` are excluded from current balance calculations but preserved in customer history for audit trails. Use in balance rebuilds and queries.

## Key Patterns & Conventions

### Entity Framework Patterns
```cs
// Global Query Filters automatically exclude soft-deleted records
modelBuilder.Entity<Order>().HasQueryFilter(o => !o.IsDeleted);

// Use IgnoreQueryFilters() for admin recovery operations
await _context.Orders.IgnoreQueryFilters().Where(o => o.IsDeleted).ToListAsync();

// Standard filtering pattern for reports
.Where(h => !h.IsDeleted) // Exclude deleted
```

### CentralFinancialService Usage
```cs
// REQUIRED for all financial operations
await _centralFinancialService.ProcessOrderCreationAsync(order, performedBy);
await _centralFinancialService.ProcessDocumentCreationAsync(document, performedBy);

// Manual balance adjustments (creates proper audit trail)
await _centralFinancialService.CreateManualCustomerBalanceHistoryAsync(
    customerId, currencyCode, amount, reason, transactionDate, performedBy);
```

### History Table Pattern
All financial entities have corresponding history tables (`CustomerBalanceHistory`, `CurrencyPoolHistory`, `BankAccountBalanceHistory`) that maintain coherent `BalanceBefore` → `TransactionAmount` → `BalanceAfter` chains.

## Development Workflow

### Build & Run Commands
```powershell
# Setup (PowerShell)
dotnet restore
dotnet ef database update  # Apply migrations
dotnet run                 # Start at http://localhost:5063

# Database operations  
dotnet ef migrations add <Name>
dotnet ef database update
dotnet ef database drop    # Reset if needed
```

### Default Credentials
```
Email: admin@iranexpedia.com
Password: Admin123!
```

## Project-Specific Conventions

### Persian RTL Interface
- All UI text in Persian with RTL Bootstrap 5
- Use `formatCurrency()` and `formatNumber()` JavaScript helpers for Persian number formatting
- Date displays use Persian calendar helpers

### Role-Based Access Control
```cs
[Authorize(Roles = "Admin,Manager,Staff")] // Orders, Currencies management
[Authorize] // General access
// Customer role: read-only access to own data
```

### Cross-Currency Rate Resolution
Rate calculation priority: `direct` → `reverse (1/rate)` → `cross via IRR base currency`

### Soft Delete Safety
- Controllers use `DeleteAsync()` methods from CentralFinancialService
- History records soft-deleted with cascading balance recalculation
- Never hard delete financial records - use `IsDeleted` flags

## Critical Integration Points

### File Upload & OCR
```cs
// Document reports with file search
IFormFile fileSearch → byte[] comparison via CompareFileData()
// OCR integration via OpenRouter API (configurable via appsettings)
```

### Real-time Notifications
```cs
// SignalR notifications for financial operations
await _notificationHub.SendManualAdjustmentNotificationAsync(title, message, eventType, userId);
```

### Balance Rebuilding
```cs
// DatabaseController → RebuildAllFinancialBalances
// Uses IsFrozen strategy: exclude frozen records from current balances
// Maintain coherent history chains with proper BalanceBefore/After calculations
```

## Common Gotchas

- **LINQ Translation**: Avoid complex operations in EF queries (e.g., `FileData.Any()`) - perform in memory after `ToListAsync()`
- **Balance Consistency**: Always use CentralFinancialService for financial operations to maintain audit trails
- **Frozen Records**: Check `IsFrozen` status in balance calculations but include in customer history for audit
- **Manual Records**: Use `TransactionType.Manual` for administrative adjustments, never `IsFrozen=true`

## Testing & Debug Tools

Access via `/Database` (Admin only):
- Balance validation and rebuilding
- Manual balance history creation
- Transaction number coverage reports
- Database rounding utilities

DatabaseController provides comprehensive financial system management and debugging capabilities.

