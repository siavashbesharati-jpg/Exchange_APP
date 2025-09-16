-- TransactionNumber Update Script for SQLite
-- This script updates TransactionNumber fields in balance history tables
-- based on AccountingDocument.ReferenceNumber values
-- UPDATED: Now handles cases where TransactionNumber is different from ReferenceNumber

-- =============================================================================
-- BEFORE RUNNING: Create a backup of your database!
-- =============================================================================

-- Check current state before update
SELECT 'CustomerBalanceHistory - Before Update' as Report;
SELECT 
    COUNT(*) as TotalAccountingDocumentRecords,
    COUNT(CASE WHEN TransactionNumber IS NOT NULL AND TransactionNumber != '' THEN 1 END) as WithTransactionNumber,
    COUNT(CASE WHEN TransactionNumber IS NULL OR TransactionNumber = '' THEN 1 END) as WithoutTransactionNumber
FROM CustomerBalanceHistory 
WHERE TransactionType = 2 -- AccountingDocument
  AND IsDeleted = 0;

SELECT 'BankAccountBalanceHistory - Before Update' as Report;
SELECT 
    COUNT(*) as TotalDocumentRecords,
    COUNT(CASE WHEN TransactionNumber IS NOT NULL AND TransactionNumber != '' THEN 1 END) as WithTransactionNumber,
    COUNT(CASE WHEN TransactionNumber IS NULL OR TransactionNumber = '' THEN 1 END) as WithoutTransactionNumber
FROM BankAccountBalanceHistory 
WHERE TransactionType = 1 -- Document
  AND IsDeleted = 0;

-- =============================================================================
-- UPDATE CustomerBalanceHistory TransactionNumber
-- =============================================================================

-- Preview what will be updated in CustomerBalanceHistory
SELECT 'CustomerBalanceHistory - Preview of Updates' as Report;
SELECT 
    cbh.Id as HistoryId,
    cbh.CustomerId,
    cbh.CurrencyCode,
    cbh.TransactionAmount,
    cbh.ReferenceId as DocumentId,
    cbh.TransactionNumber as CurrentTransactionNumber,
    ad.ReferenceNumber as NewTransactionNumber,
    ad.Title as DocumentTitle,
    CASE 
        WHEN cbh.TransactionNumber IS NULL THEN 'NULL → New'
        WHEN cbh.TransactionNumber = '' THEN 'Empty → New'
        WHEN cbh.TransactionNumber != ad.ReferenceNumber THEN 'Different → Update'
        ELSE 'Same → Skip'
    END as UpdateType
FROM CustomerBalanceHistory cbh
INNER JOIN AccountingDocuments ad ON cbh.ReferenceId = ad.Id
WHERE cbh.TransactionType = 2 -- AccountingDocument
  AND cbh.IsDeleted = 0
  AND ad.IsDeleted = 0
  AND ad.ReferenceNumber IS NOT NULL
  AND ad.ReferenceNumber != ''
  AND (cbh.TransactionNumber IS NULL 
       OR cbh.TransactionNumber = ''
       OR cbh.TransactionNumber != ad.ReferenceNumber)
ORDER BY cbh.Id
LIMIT 10; -- Show first 10 records

-- Actual update for CustomerBalanceHistory
-- UPDATED: Now updates records that are NULL, empty, OR different
UPDATE CustomerBalanceHistory 
SET TransactionNumber = (
    SELECT ad.ReferenceNumber 
    FROM AccountingDocuments ad 
    WHERE ad.Id = CustomerBalanceHistory.ReferenceId 
      AND ad.IsDeleted = 0
      AND ad.ReferenceNumber IS NOT NULL 
      AND ad.ReferenceNumber != ''
)
WHERE TransactionType = 2 -- AccountingDocument
  AND IsDeleted = 0
  AND ReferenceId IS NOT NULL
  AND EXISTS (
      SELECT 1 
      FROM AccountingDocuments ad 
      WHERE ad.Id = CustomerBalanceHistory.ReferenceId 
        AND ad.IsDeleted = 0
        AND ad.ReferenceNumber IS NOT NULL 
        AND ad.ReferenceNumber != ''
        AND (CustomerBalanceHistory.TransactionNumber IS NULL 
             OR CustomerBalanceHistory.TransactionNumber = ''
             OR CustomerBalanceHistory.TransactionNumber != ad.ReferenceNumber)
  );

-- Check how many CustomerBalanceHistory records were updated
SELECT 'CustomerBalanceHistory - Update Results' as Report;
SELECT changes() as RecordsUpdated;

-- =============================================================================
-- UPDATE BankAccountBalanceHistory TransactionNumber
-- =============================================================================

-- Preview what will be updated in BankAccountBalanceHistory
SELECT 'BankAccountBalanceHistory - Preview of Updates' as Report;
SELECT 
    babh.Id as HistoryId,
    babh.BankAccountId,
    babh.TransactionAmount,
    babh.ReferenceId as DocumentId,
    babh.TransactionNumber as CurrentTransactionNumber,
    ad.ReferenceNumber as NewTransactionNumber,
    ad.Title as DocumentTitle,
    CASE 
        WHEN babh.TransactionNumber IS NULL THEN 'NULL → New'
        WHEN babh.TransactionNumber = '' THEN 'Empty → New'
        WHEN babh.TransactionNumber != ad.ReferenceNumber THEN 'Different → Update'
        ELSE 'Same → Skip'
    END as UpdateType
FROM BankAccountBalanceHistory babh
INNER JOIN AccountingDocuments ad ON babh.ReferenceId = ad.Id
WHERE babh.TransactionType = 1 -- Document
  AND babh.IsDeleted = 0
  AND ad.IsDeleted = 0
  AND ad.ReferenceNumber IS NOT NULL
  AND ad.ReferenceNumber != ''
  AND (babh.TransactionNumber IS NULL 
       OR babh.TransactionNumber = ''
       OR babh.TransactionNumber != ad.ReferenceNumber)
ORDER BY babh.Id
LIMIT 10; -- Show first 10 records

-- Actual update for BankAccountBalanceHistory
-- UPDATED: Now updates records that are NULL, empty, OR different
UPDATE BankAccountBalanceHistory 
SET TransactionNumber = (
    SELECT ad.ReferenceNumber 
    FROM AccountingDocuments ad 
    WHERE ad.Id = BankAccountBalanceHistory.ReferenceId 
      AND ad.IsDeleted = 0
      AND ad.ReferenceNumber IS NOT NULL 
      AND ad.ReferenceNumber != ''
)
WHERE TransactionType = 1 -- Document
  AND IsDeleted = 0
  AND ReferenceId IS NOT NULL
  AND EXISTS (
      SELECT 1 
      FROM AccountingDocuments ad 
      WHERE ad.Id = BankAccountBalanceHistory.ReferenceId 
        AND ad.IsDeleted = 0
        AND ad.ReferenceNumber IS NOT NULL 
        AND ad.ReferenceNumber != ''
        AND (BankAccountBalanceHistory.TransactionNumber IS NULL 
             OR BankAccountBalanceHistory.TransactionNumber = ''
             OR BankAccountBalanceHistory.TransactionNumber != ad.ReferenceNumber)
  );

-- Check how many BankAccountBalanceHistory records were updated
SELECT 'BankAccountBalanceHistory - Update Results' as Report;
SELECT changes() as RecordsUpdated;

-- =============================================================================
-- FINAL VERIFICATION AND SUMMARY
-- =============================================================================

-- Check final state after update
SELECT 'CustomerBalanceHistory - After Update' as Report;
SELECT 
    COUNT(*) as TotalAccountingDocumentRecords,
    COUNT(CASE WHEN TransactionNumber IS NOT NULL AND TransactionNumber != '' THEN 1 END) as WithTransactionNumber,
    COUNT(CASE WHEN TransactionNumber IS NULL OR TransactionNumber = '' THEN 1 END) as WithoutTransactionNumber,
    ROUND(
        CAST(COUNT(CASE WHEN TransactionNumber IS NOT NULL AND TransactionNumber != '' THEN 1 END) AS REAL) / 
        CAST(COUNT(*) AS REAL) * 100, 2
    ) as CoveragePercentage
FROM CustomerBalanceHistory 
WHERE TransactionType = 2 -- AccountingDocument
  AND IsDeleted = 0;

SELECT 'BankAccountBalanceHistory - After Update' as Report;
SELECT 
    COUNT(*) as TotalDocumentRecords,
    COUNT(CASE WHEN TransactionNumber IS NOT NULL AND TransactionNumber != '' THEN 1 END) as WithTransactionNumber,
    COUNT(CASE WHEN TransactionNumber IS NULL OR TransactionNumber = '' THEN 1 END) as WithoutTransactionNumber,
    ROUND(
        CAST(COUNT(CASE WHEN TransactionNumber IS NOT NULL AND TransactionNumber != '' THEN 1 END) AS REAL) / 
        CAST(COUNT(*) AS REAL) * 100, 2
    ) as CoveragePercentage
FROM BankAccountBalanceHistory 
WHERE TransactionType = 1 -- Document
  AND IsDeleted = 0;

-- Show sample of updated records
SELECT 'Sample Updated CustomerBalanceHistory Records' as Report;
SELECT 
    cbh.Id,
    c.FullName as CustomerName,
    cbh.CurrencyCode,
    cbh.TransactionAmount,
    cbh.TransactionNumber,
    cbh.Description,
    cbh.TransactionDate
FROM CustomerBalanceHistory cbh
INNER JOIN Customers c ON cbh.CustomerId = c.Id
WHERE cbh.TransactionType = 2 
  AND cbh.IsDeleted = 0
  AND cbh.TransactionNumber IS NOT NULL
  AND cbh.TransactionNumber != ''
ORDER BY cbh.TransactionDate DESC
LIMIT 5;

SELECT 'Sample Updated BankAccountBalanceHistory Records' as Report;
SELECT 
    babh.Id,
    ba.BankName,
    ba.AccountNumber,
    babh.TransactionAmount,
    babh.TransactionNumber,
    babh.Description,
    babh.TransactionDate
FROM BankAccountBalanceHistory babh
INNER JOIN BankAccounts ba ON babh.BankAccountId = ba.Id
WHERE babh.TransactionType = 1 
  AND babh.IsDeleted = 0
  AND babh.TransactionNumber IS NOT NULL
  AND babh.TransactionNumber != ''
ORDER BY babh.TransactionDate DESC
LIMIT 5;

-- Show AccountingDocuments that don't have ReferenceNumber (for reference)
SELECT 'AccountingDocuments without ReferenceNumber' as Report;
SELECT 
    COUNT(*) as TotalWithoutReferenceNumber,
    COUNT(CASE WHEN ReferenceNumber IS NOT NULL AND ReferenceNumber != '' THEN 1 END) as WithReferenceNumber
FROM AccountingDocuments 
WHERE IsDeleted = 0;

-- Show conflicts where TransactionNumber differs from ReferenceNumber (before update)
SELECT 'Potential Conflicts - TransactionNumber vs ReferenceNumber' as Report;
SELECT 
    cbh.Id,
    cbh.CustomerId,
    cbh.TransactionNumber as CurrentTransactionNumber,
    ad.ReferenceNumber as DocumentReferenceNumber,
    ad.Title as DocumentTitle
FROM CustomerBalanceHistory cbh
INNER JOIN AccountingDocuments ad ON cbh.ReferenceId = ad.Id
WHERE cbh.TransactionType = 2
  AND cbh.IsDeleted = 0
  AND ad.IsDeleted = 0
  AND cbh.TransactionNumber IS NOT NULL
  AND cbh.TransactionNumber != ''
  AND ad.ReferenceNumber IS NOT NULL
  AND ad.ReferenceNumber != ''
  AND cbh.TransactionNumber != ad.ReferenceNumber
LIMIT 10;