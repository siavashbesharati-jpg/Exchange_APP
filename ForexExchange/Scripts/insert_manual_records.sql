-- Manual Customer Balance History Records Insertion Script
-- Execute this SQL script to insert the manual records
-- Date: 2025-09-24

-- Insert manual balance history records
INSERT INTO CustomerBalanceHistory 
(Id, CustomerId, CurrencyCode, TransactionType, ReferenceId, BalanceBefore, TransactionAmount, BalanceAfter, Description, TransactionDate, CreatedAt, CreatedBy, TransactionNumber, Notes, IsDeleted)
VALUES 
(1143, 38, 'IRR', 3, NULL, -5000, 5000, 0, 'جهت تراز کردن بالانس', '2025-09-21 13:42:00', '2025-09-21 10:09:39.0183089', 'Database Admin', NULL, NULL, 0),
(1068, 5, 'IRR', 3, NULL, 549341500, -1326000, 548015500, 'ضرر آقای خداداد', '2025-09-17 23:02:00', '2025-09-20 19:33:59.8319273', 'Database Admin', NULL, NULL, 0),
(1013, 12, 'IRR', 3, NULL, 0, 41000000, 41000000, 'تعدیل دستی', '2025-08-31 21:06:00', '2025-09-20 17:36:40.3508394', 'Database Admin', NULL, NULL, 0),
(1010, 20, 'OMR', 3, NULL, 0, 440, 440, 'تعدیل دستی', '2025-08-31 21:03:00', '2025-09-20 17:33:39.6532784', 'Database Admin', NULL, NULL, 0),
(1006, 12, 'OMR', 3, NULL, 0, 41, 41, 'تعدیل دستی', '2025-08-31 20:52:00', '2025-09-20 17:23:26.1773133', 'Database Admin', NULL, NULL, 0),
(943, 8, 'IRR', 3, NULL, 0, -42715000, -42715000, 'تعدیل دستی', '2025-08-31 18:23:00', '2025-09-20 14:54:05.0714444', 'Database Admin', NULL, NULL, 0),
(936, 25, 'IRR', 3, NULL, 0, -847131000, -847131000, 'تعدیل دستی', '2025-08-31 18:01:00', '2025-09-20 14:32:31.5386377', 'Database Admin', NULL, NULL, 0),
(929, 3, 'OMR', 3, NULL, 0, -2899.78, -2899.78, 'تعدیل دستی', '2025-08-31 17:52:00', '2025-09-20 14:23:18.2009196', 'Database Admin', NULL, NULL, 0),
(901, 24, 'OMR', 3, NULL, 0, -5, -5, 'تعدیل دستی', '2025-08-31 17:06:00', '2025-09-20 13:36:41.4347372', 'Database Admin', NULL, NULL, 0),
(813, 31, 'OMR', 3, NULL, 0, 137.18, 137.18, 'تعدیل دستی', '2025-08-31 11:55:00', '2025-09-20 08:25:24.7126785', 'Database Admin', NULL, NULL, 0),
(800, 31, 'AED', 3, NULL, 0, 100000, 100000, 'تعدیل دستی', '2025-08-31 09:57:00', '2025-09-20 06:25:08.7955723', 'Database Admin', NULL, NULL, 0),
(777, 5, 'IRR', 3, NULL, 0, 400000, 400000, 'تعدیل دستی', '2025-08-31 20:25:00', '2025-09-19 16:55:49.0802431', 'Database Admin', NULL, NULL, 0),
(592, 4, 'OMR', 3, NULL, 0, 10677.13, 10677.13, 'بالانس اولیه در 31 آگست', '2025-08-31 23:20:00', '2025-09-17 19:47:43.0749174', 'Database Admin', NULL, NULL, 0),
(654, 5, 'OMR', 3, NULL, 0, 60.67, 60.67, 'تعدیل دستی', '2025-08-31 22:38:00', '2025-09-18 19:05:34.8473923', 'Database Admin', NULL, NULL, 0),
(656, 30, 'OMR', 3, NULL, 3194.3, -4.5, 3189.8, 'تعدیل دستی', '2025-08-31 22:41:00', '2025-09-18 19:08:43.176507', 'Database Admin', NULL, NULL, 0),
(595, 35, 'IRR', 3, NULL, 0, 31000000, 31000000, 'بالانس اولیه در 31 آگست', '2025-08-31 20:52:00', '2025-09-18 17:20:50.0423856', 'Database Admin', NULL, NULL, 0),
(591, 4, 'AED', 3, NULL, 0, 3740, 3740, 'بالانس اولیه در تاریخ 31 آگست', '2025-08-31 23:19:00', '2025-09-17 19:46:57.5262316', 'Database Admin', NULL, NULL, 0),
(461, 30, 'OMR', 3, NULL, 0, 3194.3, 3194.3, 'بالانس اولیه 31 آگست', '2025-08-31 21:31:00', '2025-09-16 17:59:19.9557307', 'Database Admin', NULL, NULL, 0),
(448, 32, 'AED', 3, NULL, 0, 550, 550, 'بالانس اولیه در تاریخ 31 آگست', '2025-08-31 12:55:00', '2025-09-16 09:22:40.3551559', 'Database Admin', NULL, NULL, 0);

-- Verify insertion
SELECT COUNT(*) as InsertedRecords 
FROM CustomerBalanceHistory 
WHERE Id IN (1143, 1068, 1013, 1010, 1006, 943, 936, 929, 901, 813, 800, 777, 592, 654, 656, 595, 591, 461, 448);

-- Show summary
SELECT 
    CurrencyCode,
    COUNT(*) as RecordCount,
    SUM(TransactionAmount) as TotalAmount
FROM CustomerBalanceHistory 
WHERE Id IN (1143, 1068, 1013, 1010, 1006, 943, 936, 929, 901, 813, 800, 777, 592, 654, 656, 595, 591, 461, 448)
GROUP BY CurrencyCode
ORDER BY CurrencyCode;