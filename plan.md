# Incremental Balance Updates Optimization

## Problem

Currently, every operation (create/delete order, verify/delete document, manual adjustments) triggers a full rebuild of all financial balances, which becomes slow as the database grows.

## Solution Strategy

Replace full rebuilds with incremental updates for CREATE operations and targeted partial rebuilds for DELETE operations, similar to how preview methods work but actually persist the changes.

## Implementation Plan

### Phase 1: Incremental Update Helper Methods

1. **Create `GetLastHistoryBalanceAsync` helper method**

- Get last history record for customer+currency, pool+currency, or bank account
- Return current balance if no history exists
- Handle case-insensitive currency code matching
- Location: `ForexExchange/Services/CentralFinancialService.cs`

2. **Create `AddCustomerBalanceHistoryIncrementalAsync` helper method**

- Get last balance using helper above
- Calculate new balance using existing `CalculateCustomerBalanceEffects` methods
- Create history record with correct BalanceBefore/BalanceAfter chain
- Update CustomerBalance current balance
- Handle transaction ordering by TransactionDate
- Location: `ForexExchange/Services/CentralFinancialService.cs`

3. **Create `AddPoolBalanceHistoryIncrementalAsync` helper method**

- Similar to customer balance but for currency pools
- Update pool balance, buy/sell counts, totals
- Location: `ForexExchange/Services/CentralFinancialService.cs`

4. **Create `AddBankAccountBalanceHistoryIncrementalAsync` helper method**

- Similar to customer balance but for bank accounts
- Location: `ForexExchange/Services/CentralFinancialService.cs`

### Phase 2: Optimize CREATE Operations

5. **Refactor `ProcessOrderCreationAsync`**

- Remove `QueueBackgroundRebuild` call
- Use incremental update helpers for customer balances (2 currencies)
- Use incremental update helpers for pool balances (2 currencies)
- Handle frozen orders (skip updates, same as current)
- Maintain transaction safety with database transactions
- Location: `ForexExchange/Services/CentralFinancialService.cs` (line ~615)

6. **Refactor `ProcessAccountingDocumentAsync`**

- Remove `QueueBackgroundRebuild` call
- Use incremental update helpers for customer balances (payer/receiver)
- Use incremental update helpers for bank account balances (if applicable)
- Handle verified documents only
- Location: `ForexExchange/Services/CentralFinancialService.cs` (line ~657)

7. **Refactor `CreateManualCustomerBalanceHistoryAsync`**

- Remove `QueueBackgroundRebuild` call
- Use incremental update helper for customer balance
- Handle transaction date ordering
- Location: `ForexExchange/Services/CentralFinancialService.cs` (line ~1653)

8. **Refactor `CreateManualPoolBalanceHistoryAsync`**

- Remove `QueueBackgroundRebuild` call
- Use incremental update helper for pool balance
- Location: `ForexExchange/Services/CentralFinancialService.cs` (line ~1781)

9. **Refactor `CreateManualBankAccountBalanceHistoryAsync`**

- Remove `QueueBackgroundRebuild` call
- Use incremental update helper for bank account balance
- Location: `ForexExchange/Services/CentralFinancialService.cs` (line ~1905)

### Phase 3: Optimize DELETE Operations

10. **Create `RebuildAffectedBalancesFromDateAsync` helper method**

- Rebuild only specific customer+currency, pool+currency, or bank account from a transaction date forward
- Used when a transaction is deleted and subsequent balances need recalculation
- Much faster than full rebuild (only affects one entity type)
- Location: `ForexExchange/Services/CentralFinancialService.cs`

11. **Refactor `DeleteOrderAsync`**

- Mark order as deleted (existing)
- Mark related history records as deleted (soft delete)
- Call partial rebuild for affected customer+currency combinations (2 currencies)
- Call partial rebuild for affected pool+currency combinations (2 currencies)
- Location: `ForexExchange/Services/CentralFinancialService.cs` (line ~1586)

12. **Refactor `DeleteAccountingDocumentAsync`**

- Mark document as deleted (existing)
- Mark related history records as deleted (soft delete)
- Call partial rebuild for affected customer+currency combinations
- Call partial rebuild for affected bank account balances
- Location: `ForexExchange/Services/CentralFinancialService.cs` (line ~1614)

13. **Refactor `DeleteManualCustomerBalanceHistoryAsync`**

- Remove history record (hard delete for manual records)
- Call partial rebuild for affected customer+currency from transaction date
- Location: `ForexExchange/Services/CentralFinancialService.cs` (line ~1716)

14. **Refactor `DeleteManualPoolBalanceHistoryAsync`**

- Remove history record (hard delete for manual records)
- Call partial rebuild for affected pool+currency from transaction date
- Location: `ForexExchange/Services/CentralFinancialService.cs` (line ~1846)

15. **Refactor `DeleteManualBankAccountBalanceHistoryAsync`**

- Remove history record (hard delete for manual records)
- Call partial rebuild for affected bank account from transaction date
- Location: `ForexExchange/Services/CentralFinancialService.cs` (line ~1972)

### Phase 4: Remove Background Rebuild Infrastructure

16. **Remove `QueueBackgroundRebuild` method**

- No longer needed since operations are incremental and synchronous
- Location: `ForexExchange/Services/CentralFinancialService.cs` (line ~687)

17. **Keep `RebuildAllFinancialBalancesAsync` method**

- Keep as fallback/validation method
- Can be called manually for data integrity checks
- Location: `ForexExchange/Services/CentralFinancialService.cs` (line ~744)

### Phase 5: Handle Edge Cases

18. **Transaction Date Ordering**

- For new transactions, get last history record by TransactionDate (not CreatedAt)
- If new transaction date is before last record, handle out-of-order insertion
- Default: Assume new transactions have current date/time (most common case)

19. **Currency Code Normalization**

- Maintain case-insensitive matching (uppercase normalization)
- Ensure consistency across all incremental operations
- Use same normalization as preview methods

20. **Frozen Orders/Documents**

- Frozen orders don't affect current balances (skip incremental updates)
- Same logic as current implementation

21. **Transaction Safety**

- Wrap incremental updates in database transactions
- Ensure atomicity of history creation + balance updates
- Rollback on any error

### Phase 6: Testing & Validation

22. **Maintain Calculation Consistency**

- Use same calculation methods (`CalculateCustomerBalanceEffects`, `CalculateCurrencyPoolEffects`)
- Ensure incremental updates produce identical results to full rebuild
- Verify BalanceBefore/BalanceAfter chain coherence

23. **Performance Validation**

- Test with large datasets
- Verify incremental updates are faster than full rebuilds
- Measure improvement in operation response times

## Key Design Decisions

1. **Incremental Updates for CREATE**: Add history records and update balances directly without rebuild
2. **Partial Rebuilds for DELETE**: Rebuild only affected entities from transaction date forward
3. **Maintain Coherence**: Ensure BalanceBefore/BalanceAfter chains remain valid
4. **Preserve Calculation Logic**: Use existing calculation methods for consistency
5. **Keep Full Rebuild**: Maintain as fallback for data integrity validation

## Files to Modify

- `ForexExchange/Services/CentralFinancialService.cs` - Main implementation
- `ForexExchange/Services/ICentralFinancialService.cs` - Interface (if needed)

## Risks & Mitigations

1. **Risk**: Out-of-order transactions break balance chains

- **Mitigation**: Handle by ordering by TransactionDate, rebuild from insertion point if needed

2. **Risk**: Concurrent operations cause race conditions

- **Mitigation**: Use database transactions and proper locking

3. **Risk**: Calculation inconsistency between incremental and full rebuild

- **Mitigation**: Use same calculation methods, add validation tests

## Success Criteria

1. CREATE operations complete without full rebuild (incremental updates only)
2. DELETE operations use partial rebuilds (only affected entities)
3. Calculation results identical to current full rebuild approach
4. Performance improvement measurable with large datasets
5. All existing functionality preserved (frozen orders, manual adjustments, etc.)