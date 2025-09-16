using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Scripts
{
    /// <summary>
    /// Script to update TransactionNumber in balance history tables based on AccountingDocument.ReferenceNumber
    /// </summary>
    public class UpdateTransactionNumbers
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<UpdateTransactionNumbers> _logger;

        public UpdateTransactionNumbers(ForexDbContext context, ILogger<UpdateTransactionNumbers> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Updates all CustomerBalanceHistory TransactionNumber fields from AccountingDocument.ReferenceNumber
        /// </summary>
        public async Task UpdateCustomerBalanceHistoryTransactionNumbersAsync()
        {
            _logger.LogInformation("Starting CustomerBalanceHistory TransactionNumber update...");

            try
            {
                // Find all CustomerBalanceHistory records with TransactionType = AccountingDocument (2)
                // that have a ReferenceId pointing to an AccountingDocument
                var historyRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.AccountingDocument 
                               && h.ReferenceId.HasValue
                               && !h.IsDeleted) // Only non-deleted records
                    .Include(h => h.Customer) // For logging purposes
                    .ToListAsync();

                _logger.LogInformation($"Found {historyRecords.Count} CustomerBalanceHistory records to process");

                int updatedCount = 0;
                int skippedCount = 0;

                foreach (var historyRecord in historyRecords)
                {
                    try
                    {
                        // Find the corresponding AccountingDocument
                        var accountingDocument = await _context.AccountingDocuments
                            .Where(d => d.Id == historyRecord.ReferenceId.Value && !d.IsDeleted)
                            .FirstOrDefaultAsync();

                        if (accountingDocument == null)
                        {
                            _logger.LogWarning($"AccountingDocument with ID {historyRecord.ReferenceId.Value} not found for CustomerBalanceHistory ID {historyRecord.Id}");
                            skippedCount++;
                            continue;
                        }

                        // Update TransactionNumber if AccountingDocument has a ReferenceNumber
                        if (!string.IsNullOrEmpty(accountingDocument.ReferenceNumber))
                        {
                            historyRecord.TransactionNumber = accountingDocument.ReferenceNumber;
                            updatedCount++;
                            
                            _logger.LogDebug($"Updated CustomerBalanceHistory ID {historyRecord.Id} - Customer: {historyRecord.Customer?.FullName ?? "Unknown"}, TransactionNumber: {accountingDocument.ReferenceNumber}");
                        }
                        else
                        {
                            _logger.LogDebug($"AccountingDocument ID {accountingDocument.Id} has no ReferenceNumber, skipping CustomerBalanceHistory ID {historyRecord.Id}");
                            skippedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing CustomerBalanceHistory ID {historyRecord.Id}");
                        skippedCount++;
                    }
                }

                // Save all changes
                if (updatedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"CustomerBalanceHistory update completed. Updated: {updatedCount}, Skipped: {skippedCount}");
                }
                else
                {
                    _logger.LogInformation($"No CustomerBalanceHistory records updated. Skipped: {skippedCount}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating CustomerBalanceHistory TransactionNumbers");
                throw;
            }
        }

        /// <summary>
        /// Updates all BankAccountBalanceHistory TransactionNumber fields from AccountingDocument.ReferenceNumber
        /// </summary>
        public async Task UpdateBankAccountBalanceHistoryTransactionNumbersAsync()
        {
            _logger.LogInformation("Starting BankAccountBalanceHistory TransactionNumber update...");

            try
            {
                // Find all BankAccountBalanceHistory records with TransactionType = Document
                // that have a ReferenceId pointing to an AccountingDocument
                var historyRecords = await _context.BankAccountBalanceHistory
                    .Where(h => h.TransactionType == BankAccountTransactionType.Document 
                               && h.ReferenceId.HasValue
                               && !h.IsDeleted) // Only non-deleted records
                    .Include(h => h.BankAccount) // For logging purposes
                    .ToListAsync();

                _logger.LogInformation($"Found {historyRecords.Count} BankAccountBalanceHistory records to process");

                int updatedCount = 0;
                int skippedCount = 0;

                foreach (var historyRecord in historyRecords)
                {
                    try
                    {
                        // Find the corresponding AccountingDocument
                        var accountingDocument = await _context.AccountingDocuments
                            .Where(d => d.Id == historyRecord.ReferenceId.Value && !d.IsDeleted)
                            .FirstOrDefaultAsync();

                        if (accountingDocument == null)
                        {
                            _logger.LogWarning($"AccountingDocument with ID {historyRecord.ReferenceId.Value} not found for BankAccountBalanceHistory ID {historyRecord.Id}");
                            skippedCount++;
                            continue;
                        }

                        // Update TransactionNumber if AccountingDocument has a ReferenceNumber
                        if (!string.IsNullOrEmpty(accountingDocument.ReferenceNumber))
                        {
                            historyRecord.TransactionNumber = accountingDocument.ReferenceNumber;
                            updatedCount++;
                            
                            _logger.LogDebug($"Updated BankAccountBalanceHistory ID {historyRecord.Id} - Bank Account: {historyRecord.BankAccount?.BankName} {historyRecord.BankAccount?.AccountNumber}, TransactionNumber: {accountingDocument.ReferenceNumber}");
                        }
                        else
                        {
                            _logger.LogDebug($"AccountingDocument ID {accountingDocument.Id} has no ReferenceNumber, skipping BankAccountBalanceHistory ID {historyRecord.Id}");
                            skippedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing BankAccountBalanceHistory ID {historyRecord.Id}");
                        skippedCount++;
                    }
                }

                // Save all changes
                if (updatedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"BankAccountBalanceHistory update completed. Updated: {updatedCount}, Skipped: {skippedCount}");
                }
                else
                {
                    _logger.LogInformation($"No BankAccountBalanceHistory records updated. Skipped: {skippedCount}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating BankAccountBalanceHistory TransactionNumbers");
                throw;
            }
        }

        /// <summary>
        /// Updates all history tables TransactionNumber fields in a single operation
        /// </summary>
        public async Task UpdateAllHistoryTransactionNumbersAsync()
        {
            _logger.LogInformation("Starting comprehensive TransactionNumber update for all history tables...");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Update CustomerBalanceHistory
                await UpdateCustomerBalanceHistoryTransactionNumbersAsync();

                // Update BankAccountBalanceHistory
                await UpdateBankAccountBalanceHistoryTransactionNumbersAsync();

                await transaction.CommitAsync();
                _logger.LogInformation("All history table TransactionNumber updates completed successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating history table TransactionNumbers, transaction rolled back");
                throw;
            }
        }

        /// <summary>
        /// Generates a report of TransactionNumber coverage
        /// </summary>
        public async Task<TransactionNumberCoverageReport> GenerateTransactionNumberCoverageReportAsync()
        {
            _logger.LogInformation("Generating TransactionNumber coverage report...");

            var report = new TransactionNumberCoverageReport();

            // CustomerBalanceHistory statistics
            var customerHistoryTotal = await _context.CustomerBalanceHistory
                .Where(h => h.TransactionType == CustomerBalanceTransactionType.AccountingDocument && !h.IsDeleted)
                .CountAsync();

            var customerHistoryWithTransactionNumber = await _context.CustomerBalanceHistory
                .Where(h => h.TransactionType == CustomerBalanceTransactionType.AccountingDocument 
                           && !h.IsDeleted 
                           && !string.IsNullOrEmpty(h.TransactionNumber))
                .CountAsync();

            report.CustomerBalanceHistoryTotal = customerHistoryTotal;
            report.CustomerBalanceHistoryWithTransactionNumber = customerHistoryWithTransactionNumber;
            report.CustomerBalanceHistoryWithoutTransactionNumber = customerHistoryTotal - customerHistoryWithTransactionNumber;

            // BankAccountBalanceHistory statistics
            var bankHistoryTotal = await _context.BankAccountBalanceHistory
                .Where(h => h.TransactionType == BankAccountTransactionType.Document && !h.IsDeleted)
                .CountAsync();

            var bankHistoryWithTransactionNumber = await _context.BankAccountBalanceHistory
                .Where(h => h.TransactionType == BankAccountTransactionType.Document 
                           && !h.IsDeleted 
                           && !string.IsNullOrEmpty(h.TransactionNumber))
                .CountAsync();

            report.BankAccountBalanceHistoryTotal = bankHistoryTotal;
            report.BankAccountBalanceHistoryWithTransactionNumber = bankHistoryWithTransactionNumber;
            report.BankAccountBalanceHistoryWithoutTransactionNumber = bankHistoryTotal - bankHistoryWithTransactionNumber;

            // AccountingDocument statistics
            var accountingDocumentsTotal = await _context.AccountingDocuments
                .Where(d => !d.IsDeleted)
                .CountAsync();

            var accountingDocumentsWithReferenceNumber = await _context.AccountingDocuments
                .Where(d => !d.IsDeleted && !string.IsNullOrEmpty(d.ReferenceNumber))
                .CountAsync();

            report.AccountingDocumentsTotal = accountingDocumentsTotal;
            report.AccountingDocumentsWithReferenceNumber = accountingDocumentsWithReferenceNumber;
            report.AccountingDocumentsWithoutReferenceNumber = accountingDocumentsTotal - accountingDocumentsWithReferenceNumber;

            _logger.LogInformation("TransactionNumber coverage report generated");
            return report;
        }
    }

    /// <summary>
    /// Report model for TransactionNumber coverage statistics
    /// </summary>
    public class TransactionNumberCoverageReport
    {
        public int CustomerBalanceHistoryTotal { get; set; }
        public int CustomerBalanceHistoryWithTransactionNumber { get; set; }
        public int CustomerBalanceHistoryWithoutTransactionNumber { get; set; }
        public decimal CustomerBalanceHistoryCoveragePercentage => 
            CustomerBalanceHistoryTotal > 0 ? (decimal)CustomerBalanceHistoryWithTransactionNumber / CustomerBalanceHistoryTotal * 100 : 0;

        public int BankAccountBalanceHistoryTotal { get; set; }
        public int BankAccountBalanceHistoryWithTransactionNumber { get; set; }
        public int BankAccountBalanceHistoryWithoutTransactionNumber { get; set; }
        public decimal BankAccountBalanceHistoryCoveragePercentage => 
            BankAccountBalanceHistoryTotal > 0 ? (decimal)BankAccountBalanceHistoryWithTransactionNumber / BankAccountBalanceHistoryTotal * 100 : 0;

        public int AccountingDocumentsTotal { get; set; }
        public int AccountingDocumentsWithReferenceNumber { get; set; }
        public int AccountingDocumentsWithoutReferenceNumber { get; set; }
        public decimal AccountingDocumentsCoveragePercentage => 
            AccountingDocumentsTotal > 0 ? (decimal)AccountingDocumentsWithReferenceNumber / AccountingDocumentsTotal * 100 : 0;

        public override string ToString()
        {
            return $@"
TransactionNumber Coverage Report
================================

CustomerBalanceHistory (AccountingDocument type):
- Total Records: {CustomerBalanceHistoryTotal}
- With TransactionNumber: {CustomerBalanceHistoryWithTransactionNumber}
- Without TransactionNumber: {CustomerBalanceHistoryWithoutTransactionNumber}
- Coverage: {CustomerBalanceHistoryCoveragePercentage:F2}%

BankAccountBalanceHistory (Document type):
- Total Records: {BankAccountBalanceHistoryTotal}
- With TransactionNumber: {BankAccountBalanceHistoryWithTransactionNumber}
- Without TransactionNumber: {BankAccountBalanceHistoryWithoutTransactionNumber}
- Coverage: {BankAccountBalanceHistoryCoveragePercentage:F2}%

AccountingDocuments:
- Total Documents: {AccountingDocumentsTotal}
- With ReferenceNumber: {AccountingDocumentsWithReferenceNumber}
- Without ReferenceNumber: {AccountingDocumentsWithoutReferenceNumber}
- Coverage: {AccountingDocumentsCoveragePercentage:F2}%
";
        }
    }
}