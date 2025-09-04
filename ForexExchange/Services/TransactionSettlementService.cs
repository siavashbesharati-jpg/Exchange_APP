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
                if (buyOrder.FromCurrencyId != sellOrder.FromCurrencyId || buyOrder.ToCurrencyId != sellOrder.ToCurrencyId)
                    throw new InvalidOperationException("جفت ارز معاملات باید یکسان باشد");
                
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
                    FromCurrencyId = buyOrder.FromCurrencyId,
                    ToCurrencyId = buyOrder.ToCurrencyId,
                    Amount = matchedAmount,
                    Rate = rate,
                    TotalInToman = totalInToman,
                    Status = TransactionStatus.Pending,
                    CreatedAt = DateTime.Now
                };

                // Assign system bank accounts based on customer types and currencies
                await AssignSystemBankAccountsAsync(transaction, buyOrder, sellOrder);

                _context.Transactions.Add(transaction);

                // Update order statuses - orders are either Open or Completed (no partial fills)
                buyOrder.FilledAmount += matchedAmount;
                sellOrder.FilledAmount += matchedAmount;

                if (buyOrder.FilledAmount >= buyOrder.Amount)
                    buyOrder.Status = OrderStatus.Completed;
                // else stays Open

                if (sellOrder.FilledAmount >= sellOrder.Amount)
                    sellOrder.Status = OrderStatus.Completed;
                // else stays Open

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
                transaction.Notes = "فرآیند تسویه آغاز شد - در انتظار آپلود رسید دریافت وجه خریدار";

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
                transaction.Notes = $"رسید دریافت وجه خریدار تأیید شد (رسید #{receiptId}) - در انتظار انتقال فروشنده";

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
                // Orders are either Open or Completed (no partial status)
                transaction.BuyOrder.Status = transaction.BuyOrder.FilledAmount >= transaction.BuyOrder.Amount ? OrderStatus.Completed : OrderStatus.Open;
                transaction.SellOrder.Status = transaction.SellOrder.FilledAmount >= transaction.SellOrder.Amount ? OrderStatus.Completed : OrderStatus.Open;

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

        /// <summary>
        /// Assign system bank accounts to transaction based on customer types and currencies
        /// تخصیص حساب‌های بانکی سیستم به تراکنش بر اساس نوع مشتری و ارزها
        /// </summary>
        private async Task AssignSystemBankAccountsAsync(Transaction transaction, Order buyOrder, Order sellOrder)
        {
            // Get system customer and their bank accounts
            var systemCustomer = await _context.Customers
                .FirstOrDefaultAsync(c => c.IsSystem);

            if (systemCustomer == null)
            {
                _logger.LogWarning("System customer not found. Cannot assign system bank accounts.");
                return;
            }

            var systemBankAccounts = await _context.BankAccounts
                .Where(ba => ba.CustomerId == systemCustomer.Id && ba.IsActive)
                .ToListAsync();

            // Get buyer and seller customer details
            var buyerCustomer = await _context.Customers.FindAsync(buyOrder.CustomerId);
            var sellerCustomer = await _context.Customers.FindAsync(sellOrder.CustomerId);

            // Strategy: System bank accounts are used for non-system customers
            // استراتژی: حساب‌های بانکی سیستم برای مشتریان غیرسیستم استفاده می‌شود

            // For buyer (receiving currency): if buyer is NOT system customer, assign system bank as receiver
            if (buyerCustomer != null && !buyerCustomer.IsSystem)
            {
                var toCurrency = await _context.Currencies.FindAsync(transaction.ToCurrencyId);
                var systemAccountForReceiver = systemBankAccounts
                    .FirstOrDefault(ba => ba.CurrencyCode == toCurrency?.Code);

                if (systemAccountForReceiver != null)
                {
                    transaction.BuyerBankAccountId = systemAccountForReceiver.Id;
                    _logger.LogInformation($"Assigned system bank account {systemAccountForReceiver.Id} ({systemAccountForReceiver.BankName}) as receiver for buyer {buyerCustomer.FullName}");
                }
                else
                {
                    _logger.LogWarning($"No system bank account found for currency {toCurrency?.Code} to assign as receiver");
                }
            }

            // For seller (sending currency): if seller is NOT system customer, assign system bank as sender
            if (sellerCustomer != null && !sellerCustomer.IsSystem)
            {
                var fromCurrency = await _context.Currencies.FindAsync(transaction.FromCurrencyId);
                var systemAccountForSender = systemBankAccounts
                    .FirstOrDefault(ba => ba.CurrencyCode == fromCurrency?.Code);

                if (systemAccountForSender != null)
                {
                    transaction.SellerBankAccountId = systemAccountForSender.Id;
                    _logger.LogInformation($"Assigned system bank account {systemAccountForSender.Id} ({systemAccountForSender.BankName}) as sender for seller {sellerCustomer.FullName}");
                }
                else
                {
                    _logger.LogWarning($"No system bank account found for currency {fromCurrency?.Code} to assign as sender");
                }
            }

            // If both customers are system customers, assign system accounts for both sides
            if (buyerCustomer != null && buyerCustomer.IsSystem && sellerCustomer != null && sellerCustomer.IsSystem)
            {
                var fromCurrency = await _context.Currencies.FindAsync(transaction.FromCurrencyId);
                var toCurrency = await _context.Currencies.FindAsync(transaction.ToCurrencyId);

                var senderAccount = systemBankAccounts.FirstOrDefault(ba => ba.CurrencyCode == fromCurrency?.Code);
                var receiverAccount = systemBankAccounts.FirstOrDefault(ba => ba.CurrencyCode == toCurrency?.Code);

                if (senderAccount != null)
                {
                    transaction.SellerBankAccountId = senderAccount.Id;
                }
                if (receiverAccount != null)
                {
                    transaction.BuyerBankAccountId = receiverAccount.Id;
                }

                _logger.LogInformation("Both buyer and seller are system customers. Assigned system bank accounts for both sides.");
            }
        }
    }
}
