using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    public class BankAccountBalanceService : IBankAccountBalanceService
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<BankAccountBalanceService> _logger;

        public BankAccountBalanceService(ForexDbContext context, ILogger<BankAccountBalanceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<BankAccountBalance> GetBankAccountBalanceAsync(int bankAccountId, string currencyCode)
        {
            var bankAccount = await _context.BankAccounts.FindAsync(bankAccountId);
            if (bankAccount == null)
                throw new ArgumentException($"Bank account with ID {bankAccountId} not found");

            // Since each bank account now has only one currency, validate currency match
            if (bankAccount.CurrencyCode != currencyCode)
                throw new ArgumentException($"Currency mismatch: Bank account {bankAccountId} is in {bankAccount.CurrencyCode}, not {currencyCode}");

            // Return a BankAccountBalance object for compatibility
            return new BankAccountBalance
            {
                BankAccountId = bankAccountId,
                CurrencyCode = bankAccount.CurrencyCode,
                Balance = bankAccount.AccountBalance,
                LastUpdated = bankAccount.LastModified ?? bankAccount.CreatedAt,
                BankAccount = bankAccount
            };
        }

        public async Task<List<BankAccountBalance>> GetBankAccountBalancesAsync(int bankAccountId)
        {
            var bankAccount = await _context.BankAccounts.FindAsync(bankAccountId);
            if (bankAccount == null)
                return new List<BankAccountBalance>();

            // Return single balance for the account's currency
            return new List<BankAccountBalance>
            {
                new BankAccountBalance
                {
                    BankAccountId = bankAccountId,
                    CurrencyCode = bankAccount.CurrencyCode,
                    Balance = bankAccount.AccountBalance,
                    LastUpdated = bankAccount.LastModified ?? bankAccount.CreatedAt,
                    BankAccount = bankAccount
                }
            };
        }

        public async Task<BankAccountBalanceSummary> GetBankAccountBalanceSummaryAsync(int bankAccountId)
        {
            var bankAccount = await _context.BankAccounts.FindAsync(bankAccountId);
            if (bankAccount == null)
                throw new ArgumentException($"Bank account with ID {bankAccountId} not found");

            var balances = await GetBankAccountBalancesAsync(bankAccountId);

            // Calculate total balance in IRR (simplified - could use exchange rates)
            var totalInIRR = bankAccount.CurrencyCode == "IRR" ? bankAccount.AccountBalance : 0;

            return new BankAccountBalanceSummary
            {
                BankAccountId = bankAccount.Id,
                BankName = bankAccount.BankName,
                AccountNumber = bankAccount.AccountNumber,
                AccountHolderName = bankAccount.AccountHolderName,
                CurrencyBalances = balances.Where(b => b.Balance != 0).ToList(),
                TotalBalanceInIRR = totalInIRR
            };
        }

        public async Task<List<BankAccountBalanceSummary>> GetAllBankAccountBalanceSummariesAsync()
        {
            var bankAccounts = await _context.BankAccounts
                .Where(b => b.AccountBalance != 0)
                .ToListAsync();

            var summaries = new List<BankAccountBalanceSummary>();
            foreach (var account in bankAccounts)
            {
                var summary = await GetBankAccountBalanceSummaryAsync(account.Id);
                summaries.Add(summary);
            }

            return summaries.OrderBy(s => s.BankName).ThenBy(s => s.AccountNumber).ToList();
        }

        public async Task UpdateBankAccountBalanceAsync(int bankAccountId, string currencyCode, decimal amount, string reason)
        {
            var bankAccount = await _context.BankAccounts.FindAsync(bankAccountId);
            if (bankAccount == null)
                throw new ArgumentException($"Bank account with ID {bankAccountId} not found");

            // Validate currency match
            if (bankAccount.CurrencyCode != currencyCode)
                throw new ArgumentException($"Currency mismatch: Bank account {bankAccountId} is in {bankAccount.CurrencyCode}, not {currencyCode}");

            bankAccount.AccountBalance += amount;
            bankAccount.LastModified = DateTime.Now;
            bankAccount.Notes = $"{reason} - {DateTime.Now:yyyy-MM-dd HH:mm}";

            _context.BankAccounts.Update(bankAccount);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated bank account {BankAccountId} balance in {Currency}: {Amount} ({Reason})",
                bankAccountId, currencyCode, amount, reason);
        }

        public async Task ProcessAccountingDocumentAsync(AccountingDocument document)
        {
            if (document.BankAccountId.HasValue)
            {
                // Bank account is involved in the transaction
                decimal amount;
                string reason;

                if (document.PayerType == PayerType.System)
                {
                    // System is payer - money leaves bank account (negative balance)
                    amount = -document.Amount;
                    reason = $"System payment from bank - Document #{document.Id} - {document.Title}";
                }
                else // PayerType.Customer
                {
                    // Customer is payer - money comes to bank account (positive balance)
                    amount = document.Amount;
                    reason = $"Customer payment to bank - Document #{document.Id} - {document.Title}";
                }

                await UpdateBankAccountBalanceAsync(
                    document.BankAccountId.Value,
                    document.CurrencyCode,
                    amount,
                    reason
                );
            }

            _logger.LogInformation("Processed accounting document {DocumentId} for bank account", document.Id);
        }

        public async Task SetInitialBalanceAsync(int bankAccountId, string currencyCode, decimal amount, string notes)
        {
            var bankAccount = await _context.BankAccounts.FindAsync(bankAccountId);
            if (bankAccount == null)
                throw new ArgumentException($"Bank account with ID {bankAccountId} not found");

            // Validate currency match
            if (bankAccount.CurrencyCode != currencyCode)
                throw new ArgumentException($"Currency mismatch: Bank account {bankAccountId} is in {bankAccount.CurrencyCode}, not {currencyCode}");

            bankAccount.AccountBalance = amount;
            bankAccount.LastModified = DateTime.Now;
            bankAccount.Notes = $"Initial balance set: {notes}";

            _context.BankAccounts.Update(bankAccount);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Set initial balance for bank account {BankAccountId} in {Currency}: {Amount}",
                bankAccountId, currencyCode, amount);
        }
    }
}
