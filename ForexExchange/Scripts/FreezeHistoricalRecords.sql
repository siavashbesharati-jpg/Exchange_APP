-- SQL Script to freeze historical Orders and AccountingDocuments created before 2025-09-22
-- Run this after applying the AddIsFrozenToOrdersAndAccountingDocuments migration

-- Freeze Orders created before 2025-09-22
UPDATE Orders 
SET IsFrozen = 1 
WHERE CreatedAt < '2025-09-22 00:00:00.000';

-- Freeze AccountingDocuments with DocumentDate before 2025-09-22
UPDATE AccountingDocuments 
SET IsFrozen = 1 
WHERE DocumentDate < '2025-09-22 00:00:00.000';

-- Verify results
SELECT 
    'Orders' as TableName,
    COUNT(*) as TotalRecords,
    SUM(CASE WHEN IsFrozen = 1 THEN 1 ELSE 0 END) as FrozenRecords,
    SUM(CASE WHEN IsFrozen = 0 THEN 1 ELSE 0 END) as UnfrozenRecords
FROM Orders
UNION ALL
SELECT 
    'AccountingDocuments' as TableName,
    COUNT(*) as TotalRecords,
    SUM(CASE WHEN IsFrozen = 1 THEN 1 ELSE 0 END) as FrozenRecords,
    SUM(CASE WHEN IsFrozen = 0 THEN 1 ELSE 0 END) as UnfrozenRecords
FROM AccountingDocuments;