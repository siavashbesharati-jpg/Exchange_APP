# PowerShell script to remove duplicate manual transactions
# This script will execute the SQL commands step by step for safety

Write-Host "=== Remove Duplicate Manual Transactions ===" -ForegroundColor Green
Write-Host "This script will remove duplicate manual transactions keeping only the latest one" -ForegroundColor Yellow
Write-Host ""

# Database connection string (adjust if needed)
$connectionString = "Data Source=ForexExchange.db"

# Step 1: Show duplicates before deletion
Write-Host "Step 1: Checking for duplicate manual transactions..." -ForegroundColor Cyan

$step1Query = @"
SELECT 
    CustomerId, 
    CurrencyCode, 
    TransactionAmount, 
    TransactionType,
    COUNT(*) as DuplicateCount,
    MIN(Id) as FirstId,
    MAX(Id) as LastId,
    GROUP_CONCAT(Id) as AllIds
FROM CustomerBalanceHistory 
WHERE TransactionType = 3
GROUP BY CustomerId, CurrencyCode, TransactionAmount, TransactionType
HAVING COUNT(*) > 1
ORDER BY CustomerId, CurrencyCode, TransactionAmount;
"@

try {
    $duplicates = sqlite3 ForexExchange.db "$step1Query"
    if ($duplicates) {
        Write-Host "Found duplicates:" -ForegroundColor Yellow
        Write-Host $duplicates
        Write-Host ""
    } else {
        Write-Host "No duplicates found!" -ForegroundColor Green
        exit 0
    }
} catch {
    Write-Host "Error checking duplicates: $_" -ForegroundColor Red
    exit 1
}

# Step 2: Ask for confirmation
$confirmation = Read-Host "Do you want to proceed with removing duplicates? (y/N)"
if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
    Write-Host "Operation cancelled." -ForegroundColor Yellow
    exit 0
}

# Step 3: Execute deletion
Write-Host "Step 2: Removing duplicate records (keeping latest)..." -ForegroundColor Cyan

$deleteQuery = @"
WITH DuplicateRecords AS (
    SELECT 
        Id,
        ROW_NUMBER() OVER (
            PARTITION BY CustomerId, CurrencyCode, TransactionAmount, TransactionType 
            ORDER BY Id DESC
        ) as RowNum
    FROM CustomerBalanceHistory 
    WHERE TransactionType = 3
)
DELETE FROM CustomerBalanceHistory 
WHERE Id IN (
    SELECT Id 
    FROM DuplicateRecords 
    WHERE RowNum > 1
);
"@

try {
    $result = sqlite3 ForexExchange.db "$deleteQuery"
    Write-Host "Duplicates removed successfully!" -ForegroundColor Green
} catch {
    Write-Host "Error removing duplicates: $_" -ForegroundColor Red
    exit 1
}

# Step 4: Verify no more duplicates
Write-Host "Step 3: Verifying duplicates are removed..." -ForegroundColor Cyan

try {
    $remainingDuplicates = sqlite3 ForexExchange.db "$step1Query"
    if ($remainingDuplicates) {
        Write-Host "Warning: Still have duplicates:" -ForegroundColor Yellow
        Write-Host $remainingDuplicates
    } else {
        Write-Host "✅ All duplicates successfully removed!" -ForegroundColor Green
    }
} catch {
    Write-Host "Error verifying: $_" -ForegroundColor Red
}

# Step 5: Show summary
Write-Host "Step 4: Final summary..." -ForegroundColor Cyan

$summaryQuery = @"
SELECT 
    COUNT(*) as TotalManualTransactions,
    COUNT(DISTINCT CustomerId) as UniqueCustomers,
    COUNT(DISTINCT CurrencyCode) as UniqueCurrencies
FROM CustomerBalanceHistory 
WHERE TransactionType = 3;
"@

try {
    $summary = sqlite3 ForexExchange.db "$summaryQuery"
    Write-Host "Final manual transactions summary:" -ForegroundColor Green
    Write-Host $summary
} catch {
    Write-Host "Error getting summary: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Script completed!" -ForegroundColor Green