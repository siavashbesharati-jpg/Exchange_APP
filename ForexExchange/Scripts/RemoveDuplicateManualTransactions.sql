-- Remove duplicate manual transactions (TransactionType = 3)
-- Keep only the LATEST record (highest Id) for each combination of:
-- CustomerId, CurrencyCode, TransactionAmount, TransactionType

-- Step 1: Identify duplicate records (for verification)
SELECT 
    CustomerId, 
    CurrencyCode, 
    TransactionAmount, 
    TransactionType,
    COUNT(*) as DuplicateCount,
    MIN(Id) as FirstId,
    MAX(Id) as LastId,
    STRING_AGG(CAST(Id AS VARCHAR), ', ') as AllIds
FROM CustomerBalanceHistory 
WHERE TransactionType = 3 -- Manual transactions only
GROUP BY CustomerId, CurrencyCode, TransactionAmount, TransactionType
HAVING COUNT(*) > 1
ORDER BY CustomerId, CurrencyCode, TransactionAmount;

-- Step 2: Delete duplicate records (keep only the one with MAX Id)
WITH DuplicateRecords AS (
    SELECT 
        Id,
        ROW_NUMBER() OVER (
            PARTITION BY CustomerId, CurrencyCode, TransactionAmount, TransactionType 
            ORDER BY Id DESC  -- Order by Id DESC to keep the latest (highest Id)
        ) as RowNum
    FROM CustomerBalanceHistory 
    WHERE TransactionType = 3 -- Manual transactions only
)
DELETE FROM CustomerBalanceHistory 
WHERE Id IN (
    SELECT Id 
    FROM DuplicateRecords 
    WHERE RowNum > 1  -- Delete all but the first (which is the latest due to DESC order)
);

-- Step 3: Verify duplicates are removed
SELECT 
    CustomerId, 
    CurrencyCode, 
    TransactionAmount, 
    TransactionType,
    COUNT(*) as RemainingCount
FROM CustomerBalanceHistory 
WHERE TransactionType = 3 -- Manual transactions only
GROUP BY CustomerId, CurrencyCode, TransactionAmount, TransactionType
HAVING COUNT(*) > 1
ORDER BY CustomerId, CurrencyCode, TransactionAmount;

-- Step 4: Show summary of remaining manual transactions
SELECT 
    COUNT(*) as TotalManualTransactions,
    COUNT(DISTINCT CustomerId) as UniqueCustomers,
    COUNT(DISTINCT CurrencyCode) as UniqueCurrencies
FROM CustomerBalanceHistory 
WHERE TransactionType = 3;