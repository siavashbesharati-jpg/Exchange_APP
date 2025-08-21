using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    public class TransactionSettlementService : ITransactionSettlementService
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<TransactionSettlementService> _logger;
        private readonly IEmailService _emailService;
        private readonly ISettingsService _settingsService;
        private readonly ICurrencyPoolService _poolService;
        
        public TransactionSettlementService(
            ForexDbContext context, 
            ILogger<TransactionSettlementService> logger,
            IEmailService emailService,
            ISettingsService settingsService,
            ICurrencyPoolService poolService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
            _settingsService = settingsService;
            _poolService = poolService;
        }

        public async Task<Transaction> CreateTransactionAsync(Order buyOrder, Order sellOrder, decimal matchedAmount)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Validate orders can be matched
                if (buyOrder.Currency != sellOrder.Currency)
                    throw new InvalidOperationException("نوع ارز سفارشات باید یکسان باشد");
                
                if (buyOrder.Rate < sellOrder.Rate)
                    throw new InvalidOperationException("نرخ خرید باید بیشتر یا مساوی نرخ فروش باشد");

                // Calculate settlement amounts
                var rate = Math.Min(buyOrder.Rate, sellOrder.Rate); // Best rate for both parties
                var totalInToman = matchedAmount * rate;

                // Create transaction
                var transaction = new Transaction
                {
                    BuyOrderId = buyOrder.Id,
                    SellOrderId = sellOrder.Id,
                    BuyerCustomerId = buyOrder.CustomerId,
                    SellerCustomerId = sellOrder.CustomerId,
                    Currency = buyOrder.Currency,
                    Amount = matchedAmount,
                    Rate = rate,
                    TotalInToman = totalInToman,
                    Status = TransactionStatus.Pending,
                    CreatedAt = DateTime.Now
                };

                _context.Transactions.Add(transaction);

                // Update order statuses
                buyOrder.FilledAmount += matchedAmount;
                sellOrder.FilledAmount += matchedAmount;

                if (buyOrder.FilledAmount >= buyOrder.Amount)
                    buyOrder.Status = OrderStatus.Completed;
                else
                    buyOrder.Status = OrderStatus.PartiallyFilled;

                if (sellOrder.FilledAmount >= sellOrder.Amount)
                    sellOrder.Status = OrderStatus.Completed;
                else
                    sellOrder.Status = OrderStatus.PartiallyFilled;

                buyOrder.UpdatedAt = DateTime.Now;
                sellOrder.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // Update currency pool after transaction creation
                try
                {
                    await _poolService.ProcessTransactionAsync(transaction);
                    _logger.LogInformation($"Currency pool updated for transaction {transaction.Id}");
                }
                catch (Exception poolEx)
                {
                    _logger.LogError(poolEx, $"Failed to update currency pool for transaction {transaction.Id}");
                    // Don't fail the entire transaction for pool update errors
                }

                await dbTransaction.CommitAsync();

                _logger.LogInformation($"Transaction {transaction.Id} created for matched orders {buyOrder.Id} and {sellOrder.Id}");

                return transaction;
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create transaction for orders {BuyOrderId} and {SellOrderId}", buyOrder.Id, sellOrder.Id);
                throw;
            }
        }

        public async Task<bool> InitiateSettlementAsync(int transactionId)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.BuyerCustomer)
                    .Include(t => t.SellerCustomer)
                    .Include(t => t.BuyOrder)
                    .Include(t => t.SellOrder)
                    .FirstOrDefaultAsync(t => t.Id == transactionId);

                if (transaction == null)
                    return false;

                if (transaction.Status != TransactionStatus.Pending)
                    return false;

                // Update transaction status
                transaction.Status = TransactionStatus.PaymentUploaded;
                transaction.Notes = "فرآیند تسویه آغاز شد - در انتظار آپلود رسید پرداخت خریدار";

                await _context.SaveChangesAsync();

                // Send notifications
                await SendSettlementNotificationAsync(transaction, SettlementStatus.Initiated);

                _logger.LogInformation($"Settlement initiated for transaction {transactionId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate settlement for transaction {TransactionId}", transactionId);
                return false;
            }
        }

        public async Task<bool> ConfirmBuyerPaymentAsync(int transactionId, int receiptId)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.BuyerCustomer)
                    .Include(t => t.SellerCustomer)
                    .FirstOrDefaultAsync(t => t.Id == transactionId);

                var receipt = await _context.Receipts
                    .FirstOrDefaultAsync(r => r.Id == receiptId && r.TransactionId == transactionId);

                if (transaction == null || receipt == null)
                    return false;

                if (!receipt.IsVerified)
                    return false;

                // Update transaction status
                transaction.Status = TransactionStatus.ReceiptConfirmed;
                transaction.Notes = $"رسید پرداخت خریدار تأیید شد (رسید #{receiptId}) - در انتظار انتقال فروشنده";

                await _context.SaveChangesAsync();

                // Send notifications
                await SendSettlementNotificationAsync(transaction, SettlementStatus.BuyerPaymentConfirmed);

                _logger.LogInformation($"Buyer payment confirmed for transaction {transactionId} with receipt {receiptId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm buyer payment for transaction {TransactionId}", transactionId);
                return false;
            }
        }

        public async Task<bool> ConfirmSellerPaymentAsync(int transactionId, string bankReference)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.BuyerCustomer)
                    .Include(t => t.SellerCustomer)
                    .FirstOrDefaultAsync(t => t.Id == transactionId);

                if (transaction == null)
                    return false;

                if (transaction.Status != TransactionStatus.ReceiptConfirmed)
                    return false;

                // Update transaction with seller payment info
                transaction.SellerBankAccount = bankReference;
                transaction.Notes = $"انتقال فروشنده انجام شد - مرجع: {bankReference}";

                // Keep status as ReceiptConfirmed until final completion
                await _context.SaveChangesAsync();

                // Send notifications
                await SendSettlementNotificationAsync(transaction, SettlementStatus.SellerTransferConfirmed);

                _logger.LogInformation($"Seller payment confirmed for transaction {transactionId} with reference {bankReference}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to confirm seller payment for transaction {TransactionId}", transactionId);
                return false;
            }
        }

        public async Task<bool> CompleteTransactionAsync(int transactionId)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.BuyerCustomer)
                    .Include(t => t.SellerCustomer)
                    .FirstOrDefaultAsync(t => t.Id == transactionId);

                if (transaction == null)
                    return false;

                if (transaction.Status != TransactionStatus.ReceiptConfirmed)
                    return false;

                // Complete the transaction
                transaction.Status = TransactionStatus.Completed;
                transaction.CompletedAt = DateTime.Now;
                transaction.Notes = "تراکنش با موفقیت تکمیل شد";

                await _context.SaveChangesAsync();

                // Send completion notifications
                await SendSettlementNotificationAsync(transaction, SettlementStatus.Completed);

                _logger.LogInformation($"Transaction {transactionId} completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete transaction {TransactionId}", transactionId);
                return false;
            }
        }

        public async Task<bool> FailTransactionAsync(int transactionId, string reason)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.BuyOrder)
                    .Include(t => t.SellOrder)
                    .Include(t => t.BuyerCustomer)
                    .Include(t => t.SellerCustomer)
                    .FirstOrDefaultAsync(t => t.Id == transactionId);

                if (transaction == null)
                    return false;

                // Revert order statuses
                transaction.BuyOrder.FilledAmount -= transaction.Amount;
                transaction.SellOrder.FilledAmount -= transaction.Amount;

                // Update order statuses based on remaining filled amounts
                transaction.BuyOrder.Status = transaction.BuyOrder.FilledAmount > 0 ? OrderStatus.PartiallyFilled : OrderStatus.Open;
                transaction.SellOrder.Status = transaction.SellOrder.FilledAmount > 0 ? OrderStatus.PartiallyFilled : OrderStatus.Open;

                // Mark transaction as failed
                transaction.Status = TransactionStatus.Failed;
                transaction.Notes = $"تراکنش ناموفق: {reason}";
                transaction.CompletedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                // Send failure notifications
                await SendSettlementNotificationAsync(transaction, SettlementStatus.Failed);

                _logger.LogWarning($"Transaction {transactionId} failed: {reason}");
                return true;
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Failed to mark transaction {TransactionId} as failed", transactionId);
                return false;
            }
        }

        public async Task<List<Transaction>> GetPendingSettlementsAsync()
        {
            return await _context.Transactions
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .Include(t => t.BuyOrder)
                .Include(t => t.SellOrder)
                .Include(t => t.Receipts)
                .Where(t => t.Status == TransactionStatus.Pending || 
                           t.Status == TransactionStatus.PaymentUploaded || 
                           t.Status == TransactionStatus.ReceiptConfirmed)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<SettlementCalculation> CalculateSettlementAsync(Transaction transaction)
        {
            var grossAmount = transaction.TotalInToman;
            var commissionRate = await _settingsService.GetCommissionRateAsync();
            var exchangeFeeRate = await _settingsService.GetExchangeFeeRateAsync();
            
            var commissionAmount = grossAmount * commissionRate;
            var exchangeFee = grossAmount * exchangeFeeRate;
            var netAmount = grossAmount - commissionAmount - exchangeFee;

            return new SettlementCalculation
            {
                GrossAmount = grossAmount,
                CommissionRate = commissionRate,
                CommissionAmount = commissionAmount,
                ExchangeFee = exchangeFee,
                NetAmount = netAmount,
                BuyerTotalPayment = grossAmount + commissionAmount + exchangeFee,
                SellerNetReceived = grossAmount - commissionAmount - exchangeFee
            };
        }

        public async Task SendSettlementNotificationAsync(Transaction transaction, SettlementStatus status)
        {
            try
            {
                string buyerMessage = "";
                string sellerMessage = "";

                switch (status)
                {
                    case SettlementStatus.Initiated:
                        buyerMessage = $"تراکنش #{transaction.Id} آغاز شد. لطفاً رسید پرداخت خود را آپلود کنید.";
                        sellerMessage = $"تراکنش #{transaction.Id} آغاز شد. منتظر تأیید پرداخت خریدار باشید.";
                        break;
                    case SettlementStatus.BuyerPaymentConfirmed:
                        buyerMessage = $"پرداخت شما برای تراکنش #{transaction.Id} تأیید شد.";
                        sellerMessage = $"پرداخت خریدار برای تراکنش #{transaction.Id} تأیید شد. لطفاً انتقال ارز را انجام دهید.";
                        break;
                    case SettlementStatus.Completed:
                        buyerMessage = $"تراکنش #{transaction.Id} با موفقیت تکمیل شد.";
                        sellerMessage = $"تراکنش #{transaction.Id} با موفقیت تکمیل شد.";
                        break;
                    case SettlementStatus.Failed:
                        buyerMessage = $"تراکنش #{transaction.Id} ناموفق بود.";
                        sellerMessage = $"تراکنش #{transaction.Id} ناموفق بود.";
                        break;
                }

                // Send notifications (implement based on your notification system)
                await _emailService.SendEmailAsync(transaction.BuyerCustomer.Email, "وضعیت تراکنش", buyerMessage);
                await _emailService.SendEmailAsync(transaction.SellerCustomer.Email, "وضعیت تراکنش", sellerMessage);

                _logger.LogInformation($"Settlement notifications sent for transaction {transaction.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send settlement notifications for transaction {TransactionId}", transaction.Id);
            }
        }
    }
}
