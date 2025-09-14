using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange
{
    public class TestBalanceQuery
    {
        public static async Task CheckBalances(ForexDbContext context)
        {
            // Get customer balances
            var customerBalances = await context.CustomerBalances
                .Include(cb => cb.Customer)
                .OrderBy(cb => cb.CustomerId)
                .ThenBy(cb => cb.CurrencyCode)
                .ToListAsync();

            Console.WriteLine("=== Current Customer Balances ===");
            foreach (var balance in customerBalances)
            {
                Console.WriteLine($"Customer {balance.CustomerId} ({balance.Customer?.FullName}): {balance.CurrencyCode} = {balance.Balance:N0} (Updated: {balance.LastUpdated:yyyy-MM-dd HH:mm:ss})");
            }

            // Get currency pool balances  
            var poolBalances = await context.CurrencyPools
                .Include(cp => cp.Currency)
                .OrderBy(cp => cp.CurrencyCode)
                .ToListAsync();

            Console.WriteLine("\n=== Current Currency Pool Balances ===");
            foreach (var pool in poolBalances)
            {
                Console.WriteLine($"Pool {pool.CurrencyCode} ({pool.Currency?.Name}): {pool.Balance:N0} (Updated: {pool.LastUpdated:yyyy-MM-dd HH:mm:ss})");
            }

            // Check latest history records for customer 32
            var latestHistory = await context.CustomerBalanceHistory
                .Where(h => h.CustomerId == 32 && !h.IsDeleted)
                .OrderByDescending(h => h.CreatedAt)
                .Take(5)
                .ToListAsync();

            Console.WriteLine("\n=== Latest Customer 32 Balance History (Non-Deleted) ===");
            foreach (var history in latestHistory)
            {
                Console.WriteLine($"ID {history.Id}: {history.CurrencyCode} {history.TransactionAmount:N0} -> Balance: {history.BalanceAfter:N0} ({history.CreatedAt:yyyy-MM-dd HH:mm:ss}) - {history.Description}");
            }

            // Check deleted records
            var deletedHistory = await context.CustomerBalanceHistory
                .Where(h => h.CustomerId == 32 && h.IsDeleted)
                .OrderByDescending(h => h.CreatedAt)
                .Take(5)
                .ToListAsync();

            Console.WriteLine("\n=== Customer 32 Deleted Balance History ===");
            foreach (var history in deletedHistory)
            {
                Console.WriteLine($"ID {history.Id}: {history.CurrencyCode} {history.TransactionAmount:N0} -> Balance: {history.BalanceAfter:N0} ({history.CreatedAt:yyyy-MM-dd HH:mm:ss}) - DELETED by {history.DeletedBy} at {history.DeletedAt:yyyy-MM-dd HH:mm:ss}");
            }
        }
    }
}