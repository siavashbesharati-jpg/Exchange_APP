using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ForexExchange.Helpers
{
    /// <summary>
    /// A helper class to perform a one-time data rounding operation on the database.
    /// WARNING: This operation is destructive and irreversible. It permanently alters the data.
    /// </summary>
    public static class DatabaseRoundingHelper
    {
        /// <summary>
        /// Applies specific rounding logic to all monetary values in the database.
        /// - For IRR: Divides by 1000 and rounds up (Ceiling).
        /// - For other currencies: Rounds to 3 decimal places.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <returns>A summary of the changes made.</returns>
        public static async Task<string> ApplyRoundingToAllDataAsync(ForexDbContext context)
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine("Starting database rounding process...");

            // Use a transaction to ensure all changes are applied or none are.
            using (var transaction = await context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. CustomerBalances
                    var customerBalances = await context.CustomerBalances.ToListAsync();
                    foreach (var item in customerBalances)
                    {
                        item.Balance = RoundValue(item.Balance, item.CurrencyCode);
                    }
                    summary.AppendLine($"Processed {customerBalances.Count} CustomerBalances.");

                    // 2. AccountingDocuments
                    var accountingDocuments = await context.AccountingDocuments.ToListAsync();
                    foreach (var item in accountingDocuments)
                    {
                        item.Amount = RoundValue(item.Amount, item.CurrencyCode);
                    }
                    summary.AppendLine($"Processed {accountingDocuments.Count} AccountingDocuments.");

                    // 3. BankAccountBalances
                    var bankAccountBalances = await context.BankAccountBalances.ToListAsync();
                    foreach (var item in bankAccountBalances)
                    {
                        item.Balance = RoundValue(item.Balance, item.CurrencyCode);
                    }
                    summary.AppendLine($"Processed {bankAccountBalances.Count} BankAccountBalances.");

                    // 4. Orders (FromAmount and ToAmount)
                    var orders = await context.Orders.Include(o => o.FromCurrency).Include(o => o.ToCurrency).ToListAsync();
                    foreach (var item in orders)
                    {
                        if (item.FromCurrency != null)
                        {
                            item.FromAmount = RoundValue(item.FromAmount, item.FromCurrency.Code);
                        }
                        if (item.ToCurrency != null)
                        {
                            item.ToAmount = RoundValue(item.ToAmount, item.ToCurrency.Code);
                        }
                    }
                    summary.AppendLine($"Processed {orders.Count} Orders.");

                    // 5. CurrencyPools
                    var currencyPools = await context.CurrencyPools.Include(p => p.Currency).ToListAsync();
                    foreach (var item in currencyPools)
                    {
                        if (item.Currency != null)
                        {
                            item.Balance = RoundValue(item.Balance, item.Currency.Code);
                            item.TotalBought = RoundValue(item.TotalBought, item.Currency.Code);
                            item.TotalSold = RoundValue(item.TotalSold, item.Currency.Code);
                        }
                    }
                    summary.AppendLine($"Processed {currencyPools.Count} CurrencyPools.");
                    
                    // History Tables - Process with caution
                    var customerBalanceHistory = await context.CustomerBalanceHistory.ToListAsync();
                    foreach(var item in customerBalanceHistory)
                    {
                        item.BalanceBefore = RoundValue(item.BalanceBefore, item.CurrencyCode);
                        item.TransactionAmount = RoundValue(item.TransactionAmount, item.CurrencyCode);
                        item.BalanceAfter = RoundValue(item.BalanceAfter, item.CurrencyCode);
                    }
                    summary.AppendLine($"Processed {customerBalanceHistory.Count} CustomerBalanceHistory records.");

                    var currencyPoolHistory = await context.CurrencyPoolHistory.ToListAsync();
                    foreach(var item in currencyPoolHistory)
                    {
                        item.BalanceBefore = RoundValue(item.BalanceBefore, item.CurrencyCode);
                        item.TransactionAmount = RoundValue(item.TransactionAmount, item.CurrencyCode);
                        item.BalanceAfter = RoundValue(item.BalanceAfter, item.CurrencyCode);
                    }
                    summary.AppendLine($"Processed {currencyPoolHistory.Count} CurrencyPoolHistory records.");

                    var bankAccountBalanceHistory = await context.BankAccountBalanceHistory.ToListAsync();
                    foreach(var item in bankAccountBalanceHistory)
                    {
                        // This table lacks a currency code, so we can't apply IRR logic.
                        // We will apply the standard 3-decimal rounding.
                        item.BalanceBefore = RoundValue(item.BalanceBefore, null);
                        item.TransactionAmount = RoundValue(item.TransactionAmount, null);
                        item.BalanceAfter = RoundValue(item.BalanceAfter, null);
                    }
                    summary.AppendLine($"Processed {bankAccountBalanceHistory.Count} BankAccountBalanceHistory records.");


                    // Save all changes
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    summary.AppendLine("\nDatabase rounding process completed successfully and all changes have been saved.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    summary.AppendLine($"\nAn error occurred. The transaction has been rolled back. No changes were saved.");
                    summary.AppendLine($"Error: {ex.Message}");
                }
            }

            return summary.ToString();
        }

        private static decimal RoundValue(decimal value, string? currencyCode)
        {
            if (currencyCode == "IRR")
            {
                // For IRR, we round up to the nearest 1000.
                return Math.Ceiling(value / 1000) * 1000;
            }
            else
            {
                // Round to 3 decimal places for all other currencies
                return Math.Round(value, 3, MidpointRounding.AwayFromZero);
            }
        }
    }
}
