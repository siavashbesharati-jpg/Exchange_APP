using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    public class ReceiptService : IReceiptService
    {
        private readonly ForexDbContext _context;

        public ReceiptService(ForexDbContext context)
        {
            _context = context;
        }

        public async Task<Transaction?> ProcessReceiptUploadAsync(Receipt receipt)
        {
            // Validate the receipt first
            if (!await ValidateReceiptAsync(receipt))
            {
                return null;
            }

            // Get the associated order
            var order = await _context.Orders
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .FirstOrDefaultAsync(o => o.Id == receipt.OrderId);

            if (order == null)
            {
                return null;
            }

            // Determine transaction currency based on receipt type
            string transactionCurrency;
            decimal transactionAmount;

            switch (receipt.Type)
            {
                case ReceiptType.RecivreRecipt: // Customer receives money (we pay out)
                    transactionCurrency = order.ToCurrency.Code;
                    transactionAmount = receipt.Amount;
                    break;
                    
                case ReceiptType.SendRecipt: // Customer sends money (we receive)
                    transactionCurrency = order.FromCurrency.Code;
                    transactionAmount = receipt.Amount;
                    break;
                    
                default:
                    return null;
            }

            // Create the transaction
            var transaction = new Transaction
            {
                OrderId = order.Id,
                Amount = transactionAmount,
                CurrencyCode = transactionCurrency,
                TransactionDate = receipt.UploadedAt,
                Status = TransactionStatus.Completed,
                Description = $"Transaction created from {receipt.Type} receipt upload",
                ReceiptId = receipt.Id
            };

            _context.Transactions.Add(transaction);
            
            // Update receipt to mark it as processed
            receipt.IsProcessed = true;
            receipt.ProcessedAt = DateTime.Now;
            _context.Receipts.Update(receipt);

            await _context.SaveChangesAsync();

            return transaction;
        }

        public async Task<bool> ValidateReceiptAsync(Receipt receipt)
        {
            // Check if receipt exists and has required data
            if (receipt == null || receipt.Amount <= 0 || receipt.OrderId <= 0)
            {
                return false;
            }

            // Check if receipt is already processed
            if (receipt.IsProcessed)
            {
                return false;
            }

            // Check if order exists
            var orderExists = await _context.Orders
                .AnyAsync(o => o.Id == receipt.OrderId);

            return orderExists;
        }
    }
}
