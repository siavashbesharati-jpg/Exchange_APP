# CentralFinancialService - Comprehensive Documentation

## ⚠️ **CRITICAL SYSTEM COMPONENT** ⚠️

**WARNING**: This service is the core financial engine of the forex exchange system. Any modifications require extreme caution and thorough testing. All financial calculations, balance updates, and audit trails flow through this service.

## Table of Contents

1. [Overview](#overview)
2. [Core Responsibilities](#core-responsibilities)
3. [Architecture & Design](#architecture--design)
4. [Key Operations](#key-operations)
5. [Data Integrity & Safety](#data-integrity--safety)
6. [Business Logic Explained](#business-logic-explained)
7. [Common Use Cases](#common-use-cases)
8. [Safety Guidelines](#safety-guidelines)
9. [Troubleshooting](#troubleshooting)

## Overview

The `CentralFinancialService` is the centralized financial operations manager for the forex exchange system. It consolidates all financial operations that were previously scattered across multiple services while preserving **exactly the same calculation logic** to ensure zero behavioral changes.

### What It Does
- **Customer Balance Management**: Credits, debits, and balance tracking for customers
- **Currency Pool Management**: Institutional liquidity pool operations
- **Bank Account Operations**: Financial institution account management
- **Complete Audit Trail**: Immutable history of all financial transactions
- **Balance Consistency**: Validation and reconciliation of all balances
- **Preview Calculations**: Exact simulation of transaction effects before execution

### What Makes It Critical
- **Financial Integrity**: All money movements flow through this service
- **Regulatory Compliance**: Complete audit trails for financial regulations
- **Data Consistency**: Ensures all balances remain accurate and reconcilable
- **Business Continuity**: Core operations depend on this service functioning correctly

## Core Responsibilities

### 1. Customer Financial Operations
```
Customer Balance Management
├── Balance Retrieval (current balances)
├── Multi-currency Balance Operations
├── Order Processing (dual-currency impacts)
├── Document Processing (deposits/withdrawals)
└── Manual Adjustments (administrative corrections)
```

### 2. Currency Pool Management
```
Institutional Liquidity Pools
├── Pool Balance Tracking
├── Buy Operations (pool increases when buying from customers)
├── Sell Operations (pool decreases when selling to customers)
├── Manual Pool Adjustments
└── Pool History and Audit
```

### 3. Bank Account Operations
```
Financial Institution Accounts
├── Account Balance Tracking
├── Document-based Transactions
├── Balance History
└── Reconciliation Support
```

### 4. Audit and History Management
```
Complete Financial Audit Trail
├── Customer Balance History
├── Currency Pool History
├── Bank Account History
├── Balance Consistency Validation
├── Historical Data Reconciliation
└── Smart Deletion with Recalculation
```

## Architecture & Design

### Core Design Principles

1. **Event Sourcing Pattern**
   - Every financial operation creates immutable history records
   - Current balances can be recalculated from history
   - Complete audit trail for regulatory compliance

2. **Transactional Integrity**
   - All operations are database transaction-protected
   - Rollback capability if any step fails
   - Atomic operations ensure data consistency

3. **Zero Logic Changes**
   - Preserves exact calculation logic from original services
   - No behavioral modifications during centralization
   - Maintains backward compatibility

4. **Comprehensive Logging**
   - Detailed operation logging for debugging
   - Financial transaction tracking
   - Error reporting and analysis

### Database Entities Managed

```
Primary Tables:
├── CustomerBalances (current balances)
├── CurrencyPools (institutional liquidity)
└── BankAccountBalances (institution accounts)

History Tables (Audit Trail):
├── CustomerBalanceHistory
├── CurrencyPoolHistory
└── BankAccountBalanceHistory

Related Entities:
├── Orders (currency exchange transactions)
├── AccountingDocuments (financial documents)
├── Customers
├── Currencies
└── BankAccounts
```

## Key Operations

### 1. Order Processing - The Heart of Currency Exchange

**What Happens When a Customer Places an Order:**

```
Customer Order: Exchange 1000 USD → IRR at rate 42000

Customer Impact:
├── USD Balance: -1000 (customer pays USD)
└── IRR Balance: +42,000,000 (customer receives IRR)

Institution Pool Impact:
├── USD Pool: +1000 (institution receives USD from customer)
└── IRR Pool: -42,000,000 (institution provides IRR to customer)

Audit Trail:
├── 2 Customer Balance History Records
├── 2 Currency Pool History Records
└── Complete transaction logging
```

**Critical Consistency Rule**: The preview calculation (`PreviewOrderEffectsAsync`) must produce exactly the same numbers as the actual processing (`ProcessOrderCreationAsync`).

### 2. Accounting Document Processing

**Document Types and Their Effects:**

```
Customer Pays to System Document (Deposit):
├── Customer Balance: +Amount (customer's balance credited - more credit with system)
├── Bank Account: +Amount (institution's bank account credited)
└── Audit: Complete document tracking

System Pays to Customer Document (Withdrawal):
├── Customer Balance: -Amount (customer's balance debited - less credit with system)
├── Bank Account: -Amount (institution's bank account debited)
└── Audit: Complete document tracking

Transfer Document:
├── Payer Customer: +Amount (payer's balance improves - they made a payment)
├── Receiver Customer: -Amount (receiver's balance worsens - they received funds)
└── Audit: Multi-party transaction tracking
```

### 3. Balance History and Audit Trail

Every financial operation creates permanent history records:

```
History Record Contains:
├── Previous Balance
├── Transaction Amount
├── New Balance (calculated)
├── Transaction Type (Order, Document, Manual)
├── Reference ID (links to source transaction)
├── Timestamp (when operation occurred)
├── Performed By (who initiated the operation)
├── Reason (why the operation was performed)
└── Validation Status (mathematical correctness)
```

## Data Integrity & Safety

### Validation Mechanisms

1. **Mathematical Validation**
   ```
   For every history record:
   New Balance = Previous Balance + Transaction Amount
   
   If this doesn't match, the operation fails
   ```

2. **Balance Consistency Checks**
   - Current balances must match latest history records
   - Automated validation functions detect discrepancies
   - Reconciliation functions fix inconsistencies

3. **Soft Deletion Protection**
   - Deleted transactions are marked as deleted, not removed
   - Subsequent balances are recalculated after deletions
   - Complete audit trail preserved even for deleted items

### Transaction Safety

```
Database Transaction Flow:
1. Begin Transaction
2. Validate all preconditions
3. Create history record
4. Update current balance
5. Validate mathematical correctness
6. Commit transaction (or rollback if any step fails)
```

## Business Logic Explained

### Currency Exchange Logic

**Why dual-currency impact?**
Every currency exchange involves two currencies:
- Customer gives up one currency (balance decreases)
- Customer receives another currency (balance increases)
- Institution gains the currency customer gave up
- Institution loses the currency customer received

**Example Walk-through:**
```
Customer exchanges 100 USD for 4,200,000 IRR

Before Transaction:
├── Customer USD: 500
├── Customer IRR: 10,000,000
├── Institution USD Pool: 50,000
└── Institution IRR Pool: 2,000,000,000

After Transaction:
├── Customer USD: 400 (500 - 100)
├── Customer IRR: 14,200,000 (10,000,000 + 4,200,000)
├── Institution USD Pool: 50,100 (50,000 + 100)
└── Institution IRR Pool: 1,995,800,000 (2,000,000,000 - 4,200,000)
```

### Document Processing Logic

**Important**: Customer balances in this system follow **debt/credit accounting** principles:
- **Positive Balance** = Customer has money WITH the institution (customer's credit)
- **Negative Balance** = Customer owes money TO the institution (customer's debt)

**Customer Pays to System Example:**
```
Customer pays 1000 USD to the system (deposit document)

Customer Balance Impact:
└── USD Balance: +1000 (customer's balance increases - they have more credit with the system)

Bank Account Impact:
└── Institution USD Account: +1000 (institution receives the money)

Business Logic:
The customer's balance increases because they paid money to the system (increasing their credit).
The institution's bank account increases because they physically received the money.
This is a CREDIT operation for the customer (positive balance movement).
```

**System Pays to Customer Example:**
```
System pays 500 USD to the customer (withdrawal document)

Customer Balance Impact:
└── USD Balance: -500 (customer's balance decreases - they have less credit with the system)

Bank Account Impact:
└── Institution USD Account: -500 (institution pays out the money)

Business Logic:
The customer's balance decreases because the system paid money to them (decreasing their credit).
The institution's bank account decreases because they physically paid out the money.
This is a DEBIT operation for the customer (negative balance movement).
```

**Customer-to-Customer Transfer Example:**
```
Customer A transfers 200 USD to Customer B via accounting document

Customer A (Payer) Impact:
└── USD Balance: +200 (payer's balance increases - they paid/deposited)

Customer B (Receiver) Impact:
└── USD Balance: -200 (receiver's balance decreases - they received/withdrew)

Business Logic:
This follows debt/credit accounting where:
- When Customer A "pays" via the institution, their balance with the institution improves (less debt/more credit)
- When Customer B "receives" via the institution, their balance with the institution worsens (more debt/less credit)
- The institution facilitates the transfer but maintains the accounting relationship with each customer separately
```

## Common Use Cases

### 1. Customer Balance Inquiry
```csharp
// Get customer's USD balance
decimal usdBalance = await service.GetCustomerBalanceAsync(customerId: 123, currencyCode: "USD");

// Get all customer balances
var allBalances = await service.GetCustomerBalancesAsync(customerId: 123);
```

### 2. Order Preview (Before Processing)
```csharp
// Show customer what will happen if they place this order
var preview = await service.PreviewOrderEffectsAsync(order);
// Display preview.OldCustomerBalanceFrom, preview.NewCustomerBalanceFrom, etc.
```

### 3. Order Processing (Actual Transaction)
```csharp
// Process the actual order (this changes balances for real)
await service.ProcessOrderCreationAsync(order, performedBy: "Customer");
```

### 4. Administrative Balance Correction
```csharp
// Fix a balance error
await service.AdjustCustomerBalanceAsync(
    customerId: 123,
    currencyCode: "USD", 
    adjustmentAmount: 100.00m,
    reason: "Correcting data entry error from 2024-01-15",
    performedBy: "Admin.John"
);
```

### 5. Financial History Review
```csharp
// Get complete financial history for audit
var history = await service.GetCustomerFinancialHistoryAsync(
    customerId: 123,
    fromDate: DateTime.Now.AddMonths(-6),
    toDate: DateTime.Now
);
```

### 6. Balance Validation
```csharp
// Verify all balances are mathematically correct
bool isConsistent = await service.ValidateBalanceConsistencyAsync();
if (!isConsistent) {
    // Investigation needed - balances don't match history
}
```

## Safety Guidelines

### ⚠️ **CRITICAL WARNINGS**

1. **Never Modify Core Calculation Logic**
   - The mathematical operations are based on business requirements
   - Changes could cause financial discrepancies
   - Always test thoroughly in isolated environment first

2. **Preserve History Integrity**
   - Never delete history records (use soft delete only)
   - History tables provide regulatory audit trails
   - Deleted history = lost compliance evidence

3. **Maintain Preview-Actual Consistency**
   - `PreviewOrderEffectsAsync` must match `ProcessOrderCreationAsync`
   - Any change to one requires corresponding change to the other
   - UI previews must accurately reflect actual results

4. **Database Transaction Requirements**
   - All multi-step operations must be in database transactions
   - Never leave balances in inconsistent state
   - Rollback on any validation failure

### 🔧 **Development Guidelines**

1. **Testing Requirements**
   ```
   Before any changes:
   ├── Unit tests for all calculation logic
   ├── Integration tests with real database
   ├── Balance consistency validation tests
   └── Preview vs actual comparison tests
   ```

2. **Logging Requirements**
   ```
   All operations must log:
   ├── Input parameters
   ├── Calculation steps
   ├── Final results
   └── Any errors or warnings
   ```

3. **Code Review Checklist**
   ```
   Changes must verify:
   ├── Mathematical correctness
   ├── History record creation
   ├── Transaction safety
   ├── Error handling
   └── Audit trail completeness
   ```

## Troubleshooting

### Common Issues and Solutions

#### 1. Balance Inconsistency
**Symptoms**: Current balances don't match history records
```csharp
// Diagnosis
bool isConsistent = await service.ValidateBalanceConsistencyAsync();

// Solution
await service.RecalculateAllBalancesFromHistoryAsync();
```

#### 2. Preview vs Actual Mismatch
**Symptoms**: UI preview shows different amounts than actual transaction
```
Investigation Steps:
1. Compare PreviewOrderEffectsAsync and ProcessOrderCreationAsync logic
2. Check if both methods use same calculation formulas
3. Verify both methods access same data sources
4. Test with identical input data
```

#### 3. Missing History Records
**Symptoms**: Operations not appearing in audit trail
```
Check List:
├── Verify history record creation in update methods
├── Check database transaction commit
├── Ensure IsDeleted flag not set incorrectly
└── Verify foreign key relationships
```

#### 4. Currency Pool Imbalances
**Symptoms**: Currency pools show unexpected balances
```csharp
// Check pool history
var poolHistory = await service.GetCurrencyPoolHistoryAsync("USD");

// Recalculate from orders if needed
await service.RecalculateIRRPoolFromOrdersAsync();
```

### Emergency Procedures

#### Balance Reconciliation
```csharp
// Full system balance reconciliation
await service.RecalculateAllBalancesFromTransactionDatesAsync("Emergency.Reconciliation");
```

#### Data Recovery After Deletion
```csharp
// View deleted records (admin only)
var deletedOrders = await service.GetDeletedOrdersAsync();
var deletedDocs = await service.GetDeletedDocumentsAsync();

// Restore if needed
await service.RestoreOrderAsync(orderId, "Admin.Recovery");
```

## Performance Considerations

### Query Optimization
- Customer balance queries use indexed tables for fast retrieval
- History queries support date range filtering to limit data size
- Currency pool operations are optimized for frequent access

### Scalability
- History tables grow over time but maintain performance through indexing
- Bulk operations support large-scale data processing
- Transaction batching for improved throughput

### Monitoring
- Log analysis for operation performance tracking
- Balance consistency checking for proactive issue detection
- Database performance monitoring for optimization opportunities

---

## 📞 **Support and Maintenance**

This documentation covers the critical `CentralFinancialService` that manages all financial operations. For any questions or issues:

1. **First**: Review this documentation thoroughly
2. **Test**: Use development environment for any experiments
3. **Validate**: Run consistency checks after any changes
4. **Document**: Update this documentation for any modifications

**Remember**: This service handles real money and regulatory compliance. When in doubt, err on the side of caution and seek additional review before making changes.

---

*Last Updated: September 19, 2025*
*Service Version: Production-Ready*
*Author: Financial Systems Team*