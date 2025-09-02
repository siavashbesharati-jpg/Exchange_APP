using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ForexExchange.Models;
using ForexExchange.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class ReceiptsController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly IOcrService _ocrService;
        private readonly ILogger<ReceiptsController> _logger;

        public ReceiptsController(ForexDbContext context, IOcrService ocrService, ILogger<ReceiptsController> logger)
        {
            _context = context;
            _ocrService = ocrService;
            _logger = logger;
        }

        // GET: Receipts
        public async Task<IActionResult> Index()
        {
            var receipts = await _context.Receipts
                .Include(r => r.Customer)
                .Include(r => r.Order)
                .Include(r => r.Transaction)
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            return View(receipts);
        }

        // GET: Receipts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var receipt = await _context.Receipts
                .Include(r => r.Customer)
                .Include(r => r.Order)
                .Include(r => r.Transaction)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (receipt == null)
            {
                return NotFound();
            }

            return View(receipt);
        }

        // GET: Receipts/Upload
        public async Task<IActionResult> Upload(int? orderId, int? transactionId)
        {
            ViewBag.Customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();
            ViewBag.OpenOrders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .Where(o => o.Status != OrderStatus.Cancelled)
                .ToListAsync();

            // Flag to indicate if coming from settlement details (transaction-specific upload)
            ViewBag.IsTransactionSpecific = transactionId.HasValue;

            if (orderId.HasValue)
            {
                ViewBag.SelectedOrderId = orderId.Value;
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .FirstOrDefaultAsync(o => o.Id == orderId.Value);
                ViewBag.SelectedOrder = order;
                if (order != null)
                {
                    ViewBag.SelectedCustomerId = order.CustomerId;
                    ViewBag.SelectedCustomer = order.Customer;
                }
            }

            if (transactionId.HasValue)
            {
                ViewBag.SelectedTransactionId = transactionId.Value;
                var transaction = await _context.Transactions
                    .Include(t => t.BuyerCustomer)
                    .Include(t => t.SellerCustomer)
                    .Include(t => t.FromCurrency)
                    .Include(t => t.ToCurrency)
                    .Include(t => t.BuyOrder)
                    .ThenInclude(o => o.Customer)
                    .Include(t => t.BuyOrder)
                    .ThenInclude(o => o.FromCurrency)
                    .Include(t => t.BuyOrder)
                    .ThenInclude(o => o.ToCurrency)
                    .Include(t => t.SellOrder)
                    .ThenInclude(o => o.Customer)
                    .Include(t => t.SellOrder)
                    .ThenInclude(o => o.FromCurrency)
                    .Include(t => t.SellOrder)
                    .ThenInclude(o => o.ToCurrency)
                    .FirstOrDefaultAsync(t => t.Id == transactionId.Value);
                
                if (transaction != null)
                {
                    ViewBag.SelectedTransaction = transaction;
                    
                    // Set the customer based on transaction type
                    // For receipt uploads from settlements, we typically use the buyer customer
                    ViewBag.SelectedCustomerId = transaction.BuyerCustomerId;
                    ViewBag.SelectedCustomer = transaction.BuyerCustomer;
                    
                    // Set the buy order as the primary order for the receipt
                    ViewBag.SelectedOrderId = transaction.BuyOrderId;
                    ViewBag.SelectedOrder = transaction.BuyOrder;
                }
            }

            return View();
        }

        // POST: Receipts/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile receiptFile, int customerId, int? orderId, int? transactionId, ReceiptType receiptType, string? manualAmount, string? manualReference, bool skipOcr = false)
        {
            try
            {
                if (receiptFile == null || receiptFile.Length == 0)
                {
                    ModelState.AddModelError("receiptFile", "لطفاً یک فایل تصویر انتخاب کنید.");
                    await LoadUploadViewData(orderId, transactionId);
                    return View();
                }

                // Validate file type
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(receiptFile.ContentType.ToLower()))
                {
                    ModelState.AddModelError("receiptFile", "فقط فایل‌های تصویری (JPG, PNG, GIF) مجاز هستند.");
                    await LoadUploadViewData(orderId, transactionId);
                    return View();
                }

                // Validate foreign keys and normalize IDs
                // If an order is selected, enforce the receipt's customer to match the order's customer
                if (orderId.HasValue)
                {
                    var existingOrder = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId.Value);
                    if (existingOrder == null)
                    {
                        ModelState.AddModelError("orderId", "معامله انتخاب شده یافت نشد.");
                        await LoadUploadViewData(orderId, transactionId);
                        return View();
                    }
                    customerId = existingOrder.CustomerId;
                }
                else
                {
                    var customerExists = await _context.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId);
                    if (!customerExists)
                    {
                        ModelState.AddModelError("customerId", "مشتری انتخاب شده معتبر نیست.");
                        await LoadUploadViewData(orderId, transactionId);
                        return View();
                    }
                }

                if (transactionId.HasValue)
                {
                    var txExists = await _context.Transactions.AsNoTracking().AnyAsync(t => t.Id == transactionId.Value);
                    if (!txExists)
                    {
                        // If an order is provided, fall back to auto-create flow instead of erroring out
                        if (orderId.HasValue)
                        {
                            transactionId = null; // trigger auto-create/reuse logic below
                        }
                        else
                        {
                            ModelState.AddModelError("transactionId", "تراکنش انتخاب شده یافت نشد.");
                            await LoadUploadViewData(orderId, transactionId);
                            return View();
                        }
                    }
                }

                // If no transaction specified but an order is selected, auto-create or reuse a transaction
                if (!transactionId.HasValue && orderId.HasValue)
                {
                    // Try to reuse latest pending/payment-uploaded transaction for this order
                    var existingTx = await _context.Transactions
                        .Where(t => t.BuyOrderId == orderId.Value || t.SellOrderId == orderId.Value)
                        .OrderByDescending(t => t.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (existingTx != null && (existingTx.Status == TransactionStatus.Pending || existingTx.Status == TransactionStatus.PaymentUploaded))
                    {
                        transactionId = existingTx.Id;
                    }
                    else
                    {
                        // Create a minimal self-linked transaction to anchor the receipt
                        var linkedOrder = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId.Value);
                        if (linkedOrder == null)
                        {
                            ModelState.AddModelError("orderId", "معامله انتخاب شده یافت نشد.");
                            await LoadUploadViewData(orderId, transactionId);
                            return View();
                        }

                        // Compute totals
                        var totalAmount = linkedOrder.Amount * linkedOrder.Rate;

                        // Determine if ToCurrency is base currency to set TotalInToman
                        var baseCurrencyId = await _context.Currencies.Where(c => c.IsBaseCurrency).Select(c => c.Id).FirstOrDefaultAsync();
                        decimal totalInToman = 0;
                        if (baseCurrencyId > 0)
                        {
                            if (linkedOrder.FromCurrencyId == baseCurrencyId)
                            {
                                totalInToman = linkedOrder.Amount;
                            }
                            else if (linkedOrder.ToCurrencyId == baseCurrencyId)
                            {
                                totalInToman = totalAmount;
                            }
                            else
                            {
                                // Approximate via USD base (optional)
                                var usdId = await _context.Currencies.Where(c => c.Code == "USD" && c.IsActive).Select(c => c.Id).FirstOrDefaultAsync();
                                if (usdId > 0)
                                {
                                    var usdRate = await _context.ExchangeRates
                                        .Where(r => r.FromCurrencyId == baseCurrencyId && r.ToCurrencyId == usdId && r.IsActive)
                                        .Select(r => r.Rate)
                                        .FirstOrDefaultAsync();
                                    totalInToman = totalAmount * (usdRate);
                                }
                            }
                        }

                        var newTx = new Transaction
                        {
                            BuyOrderId = linkedOrder.Id,
                            SellOrderId = linkedOrder.Id, // self-link as a minimal placeholder
                            BuyerCustomerId = linkedOrder.CustomerId,
                            SellerCustomerId = linkedOrder.CustomerId,
                            FromCurrencyId = linkedOrder.FromCurrencyId,
                            ToCurrencyId = linkedOrder.ToCurrencyId,
                            Amount = linkedOrder.Amount,
                            Rate = linkedOrder.Rate,
                            TotalAmount = totalAmount,
                            TotalInToman = totalInToman,
                            Status = TransactionStatus.Pending,
                            CreatedAt = DateTime.Now,
                            Notes = "ایجاد خودکار هنگام آپلود رسید"
                        };

                        _context.Transactions.Add(newTx);
                        await _context.SaveChangesAsync();
                        transactionId = newTx.Id;
                    }
                }

                // Convert to byte array
                byte[] imageData;
                using (var memoryStream = new MemoryStream())
                {
                    await receiptFile.CopyToAsync(memoryStream);
                    imageData = memoryStream.ToArray();
                }

                // Create receipt record
                var receipt = new Receipt
                {
                    CustomerId = customerId,
                    OrderId = orderId,
                    TransactionId = transactionId,
                    Type = receiptType,
                    FileName = receiptFile.FileName,
                    ContentType = receiptFile.ContentType,
                    ImageData = imageData,
                    UploadedAt = DateTime.Now,
                    IsVerified = false
                };

                // Process OCR if not skipped
                if (!skipOcr)
                {
                    try
                    {
                        var ocrResult = await _ocrService.ProcessReceiptAsync(imageData);
                        receipt.ExtractedText = ocrResult.RawText;
                        receipt.OcrText = ocrResult.RawText; // For view compatibility
                        receipt.ParsedAmount = ocrResult.Amount;
                        receipt.ParsedReferenceId = ocrResult.ReferenceId;
                        receipt.ParsedDate = ocrResult.Date;
                        receipt.ParsedAccountNumber = ocrResult.AccountNumber;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "OCR processing failed for receipt upload");
                        receipt.Notes = "خطا در پردازش OCR: " + ex.Message;
                    }
                }
                else
                {
                    // Use manual data if OCR is skipped
                    receipt.ParsedAmount = manualAmount;
                    receipt.ParsedReferenceId = manualReference;
                    receipt.Notes = "اطلاعات به صورت دستی وارد شده است.";
                }

                _context.Add(receipt);
                await _context.SaveChangesAsync();

                // Update transaction status when receipt is uploaded
                if (transactionId.HasValue)
                {
                    var transaction = await _context.Transactions.FindAsync(transactionId.Value);
                    if (transaction != null && transaction.Status == TransactionStatus.Pending)
                    {
                        transaction.Status = TransactionStatus.PaymentUploaded;
                        _context.Update(transaction);
                        await _context.SaveChangesAsync();
                    }
                }

                TempData["SuccessMessage"] = "رسید با موفقیت آپلود شد.";
                return RedirectToAction(nameof(Details), new { id = receipt.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading receipt");
                ModelState.AddModelError("", "خطا در آپلود رسید: " + ex.Message);
                await LoadUploadViewData(orderId, transactionId);
                return View();
            }
        }

        // POST: Receipts/Verify/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id, string? verifiedAmount, string? verifiedReference, string? notes)
        {
            var receipt = await _context.Receipts.FindAsync(id);
            if (receipt == null)
            {
                return NotFound();
            }

            receipt.IsVerified = true;
            receipt.VerifiedAt = DateTime.Now;
            receipt.ParsedAmount = verifiedAmount ?? receipt.ParsedAmount;
            receipt.ParsedReferenceId = verifiedReference ?? receipt.ParsedReferenceId;
            receipt.Notes = notes;

            _context.Update(receipt);
            await _context.SaveChangesAsync();

            // Update related transaction status if applicable
            if (receipt.TransactionId.HasValue)
            {
                var transaction = await _context.Transactions.FindAsync(receipt.TransactionId.Value);
                if (transaction != null && transaction.Status == TransactionStatus.PaymentUploaded)
                {
                    // Keep transaction in PaymentUploaded status until explicit confirmation
                    // Don't auto-progress to ReceiptConfirmed here
                    _logger.LogInformation($"Receipt {id} verified for transaction {transaction.Id}. Awaiting payment confirmation.");
                }
            }

            TempData["SuccessMessage"] = "رسید تأیید شد.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Receipts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var receipt = await _context.Receipts
                .Include(r => r.Customer)
                .Include(r => r.Order)
                .Include(r => r.Transaction)
                .FirstOrDefaultAsync(r => r.Id == id);
            
            if (receipt == null)
            {
                return NotFound();
            }

            var customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();
            var orders = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.Status != OrderStatus.Cancelled)
                .ToListAsync();
            var transactions = await _context.Transactions
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .ToListAsync();

            ViewBag.CustomerOptions = customers
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = $"{c.FullName} - {c.PhoneNumber}" })
                .ToList();
            ViewBag.OrderOptions = orders
                .Select(o => new SelectListItem { Value = o.Id.ToString(), Text = $"#{o.Id} - {o.Customer?.FullName} ({o.CurrencyPair} - {o.Amount:N0})" })
                .ToList();
            ViewBag.TransactionOptions = transactions
                .Select(t => new SelectListItem { Value = t.Id.ToString(), Text = $"#{t.Id} - {t.BuyerCustomer?.FullName} / {t.SellerCustomer?.FullName}" })
                .ToList();

            return View(receipt);
        }

        // POST: Receipts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Receipt receipt, IFormFile? newReceiptFile)
        {
            if (id != receipt.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingReceipt = await _context.Receipts.FindAsync(id);
                    if (existingReceipt == null)
                    {
                        return NotFound();
                    }

                    // Update editable fields
                    existingReceipt.CustomerId = receipt.CustomerId;
                    existingReceipt.OrderId = receipt.OrderId;
                    existingReceipt.TransactionId = receipt.TransactionId;
                    existingReceipt.Type = receipt.Type;
                    existingReceipt.ParsedAmount = receipt.ParsedAmount;
                    existingReceipt.ParsedReferenceId = receipt.ParsedReferenceId;
                    existingReceipt.ParsedDate = receipt.ParsedDate;
                    existingReceipt.ParsedAccountNumber = receipt.ParsedAccountNumber;
                    existingReceipt.Notes = receipt.Notes;

                    // Handle new file upload if provided
                    if (newReceiptFile != null && newReceiptFile.Length > 0)
                    {
                        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                        if (allowedTypes.Contains(newReceiptFile.ContentType.ToLower()))
                        {
                            using (var memoryStream = new MemoryStream())
                            {
                                await newReceiptFile.CopyToAsync(memoryStream);
                                existingReceipt.ImageData = memoryStream.ToArray();
                                existingReceipt.FileName = newReceiptFile.FileName;
                                existingReceipt.ContentType = newReceiptFile.ContentType;
                            }
                        }
                    }

                    _context.Update(existingReceipt);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "رسید با موفقیت بروزرسانی شد.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReceiptExists(receipt.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            var customers2 = await _context.Customers.Where(c => c.IsActive).ToListAsync();
            var orders2 = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.Status != OrderStatus.Cancelled)
                .ToListAsync();
            var transactions2 = await _context.Transactions
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .ToListAsync();

            ViewBag.CustomerOptions = customers2
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = $"{c.FullName} - {c.PhoneNumber}" })
                .ToList();
            ViewBag.OrderOptions = orders2
                .Select(o => new SelectListItem { Value = o.Id.ToString(), Text = $"#{o.Id} - {o.Customer?.FullName} ({o.CurrencyPair} - {o.Amount:N0})" })
                .ToList();
            ViewBag.TransactionOptions = transactions2
                .Select(t => new SelectListItem { Value = t.Id.ToString(), Text = $"#{t.Id} - {t.BuyerCustomer?.FullName} / {t.SellerCustomer?.FullName}" })
                .ToList();

            return View(receipt);
        }

        // GET: Receipts/Image/5
        public async Task<IActionResult> Image(int id)
        {
            var receipt = await _context.Receipts.FindAsync(id);
            if (receipt == null || receipt.ImageData == null)
            {
                return NotFound();
            }

            return File(receipt.ImageData, receipt.ContentType);
        }

        // POST: Receipts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var receipt = await _context.Receipts.FindAsync(id);
            if (receipt != null)
            {
                _context.Receipts.Remove(receipt);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "رسید حذف شد.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadUploadViewData(int? orderId, int? transactionId)
        {
            ViewBag.Customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();
            ViewBag.OpenOrders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .Where(o => o.Status != OrderStatus.Cancelled)
                .ToListAsync();

            // Flag to indicate if coming from settlement details (transaction-specific upload)
            ViewBag.IsTransactionSpecific = transactionId.HasValue;

            if (orderId.HasValue)
            {
                ViewBag.SelectedOrderId = orderId.Value;
                var order = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .FirstOrDefaultAsync(o => o.Id == orderId.Value);
                ViewBag.SelectedOrder = order;
            }

            if (transactionId.HasValue)
            {
                ViewBag.SelectedTransactionId = transactionId.Value;
                var transaction = await _context.Transactions
                    .Include(t => t.BuyerCustomer)
                    .Include(t => t.SellerCustomer)
                    .Include(t => t.FromCurrency)
                    .Include(t => t.ToCurrency)
                    .Include(t => t.BuyOrder)
                    .ThenInclude(o => o.Customer)
                    .Include(t => t.BuyOrder)
                    .ThenInclude(o => o.FromCurrency)
                    .Include(t => t.BuyOrder)
                    .ThenInclude(o => o.ToCurrency)
                    .Include(t => t.SellOrder)
                    .ThenInclude(o => o.Customer)
                    .Include(t => t.SellOrder)
                    .ThenInclude(o => o.FromCurrency)
                    .Include(t => t.SellOrder)
                    .ThenInclude(o => o.ToCurrency)
                    .FirstOrDefaultAsync(t => t.Id == transactionId.Value);
                
                if (transaction != null)
                {
                    ViewBag.SelectedTransaction = transaction;
                    
                    // Set the customer based on transaction type
                    // For receipt uploads from settlements, we typically use the buyer customer
                    ViewBag.SelectedCustomerId = transaction.BuyerCustomerId;
                    ViewBag.SelectedCustomer = transaction.BuyerCustomer;
                    
                    // Set the buy order as the primary order for the receipt
                    ViewBag.SelectedOrderId = transaction.BuyOrderId;
                    ViewBag.SelectedOrder = transaction.BuyOrder;
                }
            }
        }

        private bool ReceiptExists(int id)
        {
            return _context.Receipts.Any(e => e.Id == id);
        }
    }
}
