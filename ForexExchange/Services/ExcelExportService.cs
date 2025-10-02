using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using ForexExchange.Models;

namespace ForexExchange.Services
{
    public class ExcelExportService
    {
        public byte[] GenerateCustomerTimelineExcel(string customerName, List<object> transactions, Dictionary<string, decimal> finalBalances, DateTime? fromDate = null, DateTime? toDate = null)
        {
            // Set license context for EPPlus 6
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage();

            // Group transactions by currency
            var transactionsByCurrency = transactions
                .Cast<dynamic>()
                .GroupBy(t => t.CurrencyCode?.ToString() ?? "Unknown")
                .OrderBy(g => g.Key)
                .ToList();

            // Add main summary sheet first
            CreateSummarySheet(package, customerName, finalBalances, fromDate, toDate, transactionsByCurrency.Count);

            // Create a worksheet for each currency
            foreach (var currencyGroup in transactionsByCurrency)
            {
                var currency = currencyGroup.Key;
                var currencyTransactions = currencyGroup.OrderByDescending(t => DateTime.Parse(t.TransactionDate.ToString())).ToList();
                
                CreateCurrencySheet(package, customerName, currency, currencyTransactions, finalBalances.ContainsKey(currency) ? finalBalances[currency] : 0, fromDate, toDate);
            }

            return package.GetAsByteArray();
        }

        private void CreateSummarySheet(ExcelPackage package, string customerName, Dictionary<string, decimal> finalBalances, DateTime? fromDate, DateTime? toDate, int currencyCount)
        {
            var worksheet = package.Workbook.Worksheets.Add("خلاصه گزارش");
            worksheet.View.RightToLeft = true;

            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = "خلاصه گزارش مالی مشتری";
            worksheet.Cells[row, 1, row, 4].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 4], 16, true);
            row += 2;

            // Customer name
            worksheet.Cells[row, 1].Value = "نام مشتری:";
            worksheet.Cells[row, 2].Value = customerName;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Date range
            var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
            var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
            worksheet.Cells[row, 1].Value = "بازه زمانی:";
            worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Number of currencies
            worksheet.Cells[row, 1].Value = "تعداد ارزها:";
            worksheet.Cells[row, 2].Value = $"{currencyCount} ارز";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row += 2;

            // Final balances header
            worksheet.Cells[row, 1].Value = "موجودی نهایی ارزها";
            worksheet.Cells[row, 1, row, 3].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 3], 14, true);
            row++;

            // Balance table header
            worksheet.Cells[row, 1].Value = "ارز";
            worksheet.Cells[row, 2].Value = "موجودی";
            worksheet.Cells[row, 3].Value = "برگه مربوطه";
            StyleHeaderRow(worksheet.Cells[row, 1, row, 3]);
            row++;

            // Final balances
            foreach (var balance in finalBalances.OrderBy(b => b.Key))
            {
                worksheet.Cells[row, 1].Value = balance.Key;
                worksheet.Cells[row, 2].Value = balance.Value.ToString("N0");
                worksheet.Cells[row, 3].Value = $"جزئیات {balance.Key}";
                
                StyleDataRow(worksheet.Cells[row, 1, row, 3]);
                worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0";
                
                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        private void CreateCurrencySheet(ExcelPackage package, string customerName, string currency, List<dynamic> transactions, decimal finalBalance, DateTime? fromDate, DateTime? toDate)
        {
            var worksheet = package.Workbook.Worksheets.Add($"جزئیات {currency}");
            worksheet.View.RightToLeft = true;

            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = $"گزارش مالی - {currency}";
            worksheet.Cells[row, 1, row, 6].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 6], 16, true);
            row += 2;

            // Customer name
            worksheet.Cells[row, 1].Value = "نام مشتری:";
            worksheet.Cells[row, 2].Value = customerName;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Currency
            worksheet.Cells[row, 1].Value = "ارز:";
            worksheet.Cells[row, 2].Value = currency;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Date range
            var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
            var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
            worksheet.Cells[row, 1].Value = "بازه زمانی:";
            worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Final balance
            worksheet.Cells[row, 1].Value = "موجودی نهایی:";
            worksheet.Cells[row, 2].Value = finalBalance.ToString("N0");
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0";
            row += 2;

            // Transaction count
            worksheet.Cells[row, 1].Value = "تعداد تراکنش:";
            worksheet.Cells[row, 2].Value = $"{transactions.Count} تراکنش";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row += 2;

            // Transactions header
            worksheet.Cells[row, 1].Value = "تاریخ";
            worksheet.Cells[row, 2].Value = "نوع تراکنش";
            worksheet.Cells[row, 3].Value = "مبلغ";
            worksheet.Cells[row, 4].Value = "موجودی پس از تراکنش";
            worksheet.Cells[row, 5].Value = "شماره مرجع";
            worksheet.Cells[row, 6].Value = "شرح";

            StyleHeaderRow(worksheet.Cells[row, 1, row, 6]);
            row++;

            // Transaction data
            foreach (var transaction in transactions)
            {
                worksheet.Cells[row, 1].Value = DateTime.Parse(transaction.TransactionDate.ToString()).ToString("yyyy/MM/dd HH:mm");
                worksheet.Cells[row, 2].Value = GetTransactionTypeText(transaction.Type?.ToString());
                worksheet.Cells[row, 3].Value = decimal.Parse(transaction.Amount?.ToString() ?? "0");
                worksheet.Cells[row, 4].Value = decimal.Parse(transaction.RunningBalance?.ToString() ?? "0");
                worksheet.Cells[row, 5].Value = transaction.ReferenceId?.ToString();
                worksheet.Cells[row, 6].Value = transaction.Description?.ToString();

                // Style data row
                StyleDataRow(worksheet.Cells[row, 1, row, 6]);
                
                // Apply conditional formatting for transaction type
                StyleTransactionTypeCell(worksheet.Cells[row, 2], transaction.Type?.ToString());
                
                // Format currency columns
                worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0";
                
                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        public byte[] GenerateDocumentsExcel(List<object> documents, DateTime? fromDate = null, DateTime? toDate = null, string? currency = null, string? customer = null)
        {
            // Set license context for EPPlus 6
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage();

            // Group documents by currency
            var documentsByCurrency = documents
                .Cast<dynamic>()
                .GroupBy(d => d.currencyCode?.ToString() ?? "Unknown")
                .OrderBy(g => g.Key)
                .ToList();

            // Add main summary sheet first
            CreateDocumentsSummarySheet(package, documentsByCurrency.Count, fromDate, toDate, currency, customer);

            // Create a worksheet for each currency
            foreach (var currencyGroup in documentsByCurrency)
            {
                var currencyCode = currencyGroup.Key;
                var currencyDocuments = currencyGroup.OrderByDescending(d => DateTime.Parse(d.date.ToString())).ToList();
                
                CreateDocumentsCurrencySheet(package, currencyCode, currencyDocuments, fromDate, toDate, customer);
            }

            return package.GetAsByteArray();
        }

        private void CreateDocumentsSummarySheet(ExcelPackage package, int currencyCount, DateTime? fromDate, DateTime? toDate, string? currency, string? customer)
        {
            var worksheet = package.Workbook.Worksheets.Add("خلاصه اسناد");
            worksheet.View.RightToLeft = true;

            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = "خلاصه گزارش اسناد حسابداری";
            worksheet.Cells[row, 1, row, 4].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 4], 16, true);
            row += 2;

            // Filters applied
            worksheet.Cells[row, 1].Value = "فیلترهای اعمال شده:";
            StyleInfoCell(worksheet.Cells[row, 1]);
            row++;

            if (fromDate.HasValue || toDate.HasValue)
            {
                var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
                var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
                worksheet.Cells[row, 1].Value = "بازه زمانی:";
                worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
                StyleInfoCell(worksheet.Cells[row, 1]);
                StyleInfoCell(worksheet.Cells[row, 2]);
                row++;
            }

            if (!string.IsNullOrEmpty(currency))
            {
                worksheet.Cells[row, 1].Value = "ارز:";
                worksheet.Cells[row, 2].Value = currency;
                StyleInfoCell(worksheet.Cells[row, 1]);
                StyleInfoCell(worksheet.Cells[row, 2]);
                row++;
            }

            if (!string.IsNullOrEmpty(customer))
            {
                worksheet.Cells[row, 1].Value = "مشتری:";
                worksheet.Cells[row, 2].Value = customer;
                StyleInfoCell(worksheet.Cells[row, 1]);
                StyleInfoCell(worksheet.Cells[row, 2]);
                row++;
            }

            // Number of currencies
            worksheet.Cells[row, 1].Value = "تعداد ارزها:";
            worksheet.Cells[row, 2].Value = $"{currencyCount} ارز";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row += 2;

            // Currency summary header
            worksheet.Cells[row, 1].Value = "خلاصه ارزها";
            worksheet.Cells[row, 1, row, 3].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 3], 14, true);
            row++;

            // Summary table header
            worksheet.Cells[row, 1].Value = "ارز";
            worksheet.Cells[row, 2].Value = "تعداد اسناد";
            worksheet.Cells[row, 3].Value = "برگه مربوطه";
            StyleHeaderRow(worksheet.Cells[row, 1, row, 3]);
            row++;

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        private void CreateDocumentsCurrencySheet(ExcelPackage package, string currency, List<dynamic> documents, DateTime? fromDate, DateTime? toDate, string? customer)
        {
            var worksheet = package.Workbook.Worksheets.Add($"اسناد {currency}");
            worksheet.View.RightToLeft = true;

            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = $"گزارش اسناد - {currency}";
            worksheet.Cells[row, 1, row, 7].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 7], 16, true);
            row += 2;

            // Currency
            worksheet.Cells[row, 1].Value = "ارز:";
            worksheet.Cells[row, 2].Value = currency;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Date range
            var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
            var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
            worksheet.Cells[row, 1].Value = "بازه زمانی:";
            worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            if (!string.IsNullOrEmpty(customer))
            {
                worksheet.Cells[row, 1].Value = "مشتری:";
                worksheet.Cells[row, 2].Value = customer;
                StyleInfoCell(worksheet.Cells[row, 1]);
                StyleInfoCell(worksheet.Cells[row, 2]);
                row++;
            }

            // Document count
            worksheet.Cells[row, 1].Value = "تعداد اسناد:";
            worksheet.Cells[row, 2].Value = $"{documents.Count} سند";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row += 2;

            // Documents header (removed وضعیت and تاریخ ایجاد, moved شرح to end)
            worksheet.Cells[row, 1].Value = "تاریخ سند";
            worksheet.Cells[row, 2].Value = "نوع سند";
            worksheet.Cells[row, 3].Value = "مبلغ";
            worksheet.Cells[row, 4].Value = "شماره مرجع";
            worksheet.Cells[row, 5].Value = "پرداخت کننده";
            worksheet.Cells[row, 6].Value = "دریافت کننده";
            worksheet.Cells[row, 7].Value = "شرح";

            StyleHeaderRow(worksheet.Cells[row, 1, row, 7]);
            row++;

            // Document data
            foreach (var document in documents)
            {
                // Add timestamp to document date
                worksheet.Cells[row, 1].Value = DateTime.Parse(document.date.ToString()).ToString("yyyy/MM/dd HH:mm");
                worksheet.Cells[row, 2].Value = document.documentType?.ToString();
                worksheet.Cells[row, 3].Value = decimal.Parse(document.amount?.ToString() ?? "0");
                worksheet.Cells[row, 4].Value = document.referenceNumber?.ToString();
                worksheet.Cells[row, 5].Value = document.payerName?.ToString();
                worksheet.Cells[row, 6].Value = document.receiverName?.ToString();
                worksheet.Cells[row, 7].Value = document.description?.ToString(); // شرح moved to last column

                // Style data row
                StyleDataRow(worksheet.Cells[row, 1, row, 7]);
                
                // Format currency column
                worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                
                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        public byte[] GeneratePoolTimelineExcel(string currencyCode, List<object> transactions, DateTime? fromDate = null, DateTime? toDate = null)
        {
            // Set license context for EPPlus 6
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("گزارش صندوق");

            // Set worksheet direction to RTL
            worksheet.View.RightToLeft = true;

            // Add header information
            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = "گزارش صندوق";
            worksheet.Cells[row, 1, row, 8].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 8], 16, true);
            row += 2;

            // Currency
            worksheet.Cells[row, 1].Value = "ارز:";
            worksheet.Cells[row, 2].Value = currencyCode;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Date range
            var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
            var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
            worksheet.Cells[row, 1].Value = "بازه زمانی:";
            worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row += 2;

            // Transactions header
            worksheet.Cells[row, 1].Value = "تاریخ";
            worksheet.Cells[row, 2].Value = "نوع تراکنش";
            worksheet.Cells[row, 3].Value = "مبلغ";
            worksheet.Cells[row, 4].Value = "شرح";
            worksheet.Cells[row, 5].Value = "موجودی پس از تراکنش";
            worksheet.Cells[row, 6].Value = "شماره مرجع";
            worksheet.Cells[row, 7].Value = "وضعیت";
            worksheet.Cells[row, 8].Value = "یادداشت";

            StyleHeaderRow(worksheet.Cells[row, 1, row, 8]);
            row++;

            // Transaction data
            foreach (dynamic transaction in transactions)
            {
                worksheet.Cells[row, 1].Value = DateTime.Parse(transaction.date.ToString()).ToString("yyyy/MM/dd");
                worksheet.Cells[row, 2].Value = transaction.type?.ToString();
                worksheet.Cells[row, 3].Value = decimal.Parse(transaction.amount?.ToString() ?? "0");
                worksheet.Cells[row, 4].Value = transaction.description?.ToString();
                worksheet.Cells[row, 5].Value = decimal.Parse(transaction.runningBalance?.ToString() ?? "0");
                worksheet.Cells[row, 6].Value = transaction.referenceId?.ToString();
                worksheet.Cells[row, 7].Value = "تایید شده";
                worksheet.Cells[row, 8].Value = transaction.note?.ToString();

                // Style data row
                StyleDataRow(worksheet.Cells[row, 1, row, 8]);
                
                // Format currency columns
                worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0";
                
                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            return package.GetAsByteArray();
        }

        public byte[] GenerateBankAccountTimelineExcel(string bankAccountName, List<object> transactions, DateTime? fromDate = null, DateTime? toDate = null)
        {
            // Set license context for EPPlus 6
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("گزارش حساب بانکی");

            // Set worksheet direction to RTL
            worksheet.View.RightToLeft = true;

            // Add header information
            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = "گزارش حساب بانکی";
            worksheet.Cells[row, 1, row, 8].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 8], 16, true);
            row += 2;

            // Bank Account
            worksheet.Cells[row, 1].Value = "حساب بانکی:";
            worksheet.Cells[row, 2].Value = bankAccountName;
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row++;

            // Date range
            var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
            var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
            worksheet.Cells[row, 1].Value = "بازه زمانی:";
            worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
            StyleInfoCell(worksheet.Cells[row, 1]);
            StyleInfoCell(worksheet.Cells[row, 2]);
            row += 2;

            // Transactions header
            worksheet.Cells[row, 1].Value = "تاریخ";
            worksheet.Cells[row, 2].Value = "نوع تراکنش";
            worksheet.Cells[row, 3].Value = "مبلغ";
            worksheet.Cells[row, 4].Value = "شرح";
            worksheet.Cells[row, 5].Value = "موجودی پس از تراکنش";
            worksheet.Cells[row, 6].Value = "شماره مرجع";
            worksheet.Cells[row, 7].Value = "وضعیت";
            worksheet.Cells[row, 8].Value = "یادداشت";

            StyleHeaderRow(worksheet.Cells[row, 1, row, 8]);
            row++;

            // Transaction data
            foreach (dynamic transaction in transactions)
            {
                worksheet.Cells[row, 1].Value = DateTime.Parse(transaction.date.ToString()).ToString("yyyy/MM/dd");
                worksheet.Cells[row, 2].Value = transaction.type?.ToString();
                worksheet.Cells[row, 3].Value = decimal.Parse(transaction.amount?.ToString() ?? "0");
                worksheet.Cells[row, 4].Value = transaction.description?.ToString();
                worksheet.Cells[row, 5].Value = decimal.Parse(transaction.runningBalance?.ToString() ?? "0");
                worksheet.Cells[row, 6].Value = transaction.referenceId?.ToString();
                worksheet.Cells[row, 7].Value = "تایید شده";
                worksheet.Cells[row, 8].Value = transaction.note?.ToString();

                // Style data row
                StyleDataRow(worksheet.Cells[row, 1, row, 8]);
                
                // Format currency columns
                worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0";
                worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0";
                
                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            return package.GetAsByteArray();
        }

        public byte[] GenerateOrdersExcel(List<object> orders, DateTime? fromDate = null, DateTime? toDate = null, string? fromCurrency = null, string? toCurrency = null, string? orderStatus = null)
        {
            // Set license context for EPPlus 6
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("گزارش معاملات");

            // Set worksheet direction to RTL
            worksheet.View.RightToLeft = true;

            // Add header information
            int row = 1;
            
            // Title
            worksheet.Cells[row, 1].Value = "گزارش معاملات";
            worksheet.Cells[row, 1, row, 9].Merge = true;
            StyleHeaderCell(worksheet.Cells[row, 1, row, 9], 16, true);
            row += 2;

            // Filters applied
            if (fromDate.HasValue || toDate.HasValue || !string.IsNullOrEmpty(fromCurrency) || !string.IsNullOrEmpty(toCurrency) || !string.IsNullOrEmpty(orderStatus))
            {
                worksheet.Cells[row, 1].Value = "فیلترهای اعمال شده:";
                StyleInfoCell(worksheet.Cells[row, 1]);
                row++;

                if (fromDate.HasValue || toDate.HasValue)
                {
                    var fromDateStr = fromDate?.ToString("yyyy/MM/dd") ?? "ابتدای زمان";
                    var toDateStr = toDate?.ToString("yyyy/MM/dd") ?? "انتهای زمان";
                    worksheet.Cells[row, 1].Value = "بازه زمانی:";
                    worksheet.Cells[row, 2].Value = $"{fromDateStr} تا {toDateStr}";
                    StyleInfoCell(worksheet.Cells[row, 1]);
                    StyleInfoCell(worksheet.Cells[row, 2]);
                    row++;
                }

                if (!string.IsNullOrEmpty(fromCurrency))
                {
                    worksheet.Cells[row, 1].Value = "ارز مبدأ:";
                    worksheet.Cells[row, 2].Value = fromCurrency;
                    StyleInfoCell(worksheet.Cells[row, 1]);
                    StyleInfoCell(worksheet.Cells[row, 2]);
                    row++;
                }

                if (!string.IsNullOrEmpty(toCurrency))
                {
                    worksheet.Cells[row, 1].Value = "ارز مقصد:";
                    worksheet.Cells[row, 2].Value = toCurrency;
                    StyleInfoCell(worksheet.Cells[row, 1]);
                    StyleInfoCell(worksheet.Cells[row, 2]);
                    row++;
                }

                if (!string.IsNullOrEmpty(orderStatus))
                {
                    worksheet.Cells[row, 1].Value = "وضعیت:";
                    worksheet.Cells[row, 2].Value = orderStatus;
                    StyleInfoCell(worksheet.Cells[row, 1]);
                    StyleInfoCell(worksheet.Cells[row, 2]);
                    row++;
                }
                row++;
            }

            // Orders header
            worksheet.Cells[row, 1].Value = "شناسه";
            worksheet.Cells[row, 2].Value = "تاریخ";
            worksheet.Cells[row, 3].Value = "مشتری";
            worksheet.Cells[row, 4].Value = "از ارز";
            worksheet.Cells[row, 5].Value = "مبلغ";
            worksheet.Cells[row, 6].Value = "به ارز";
            worksheet.Cells[row, 7].Value = "نرخ تبدیل";
            worksheet.Cells[row, 8].Value = "مبلغ نهایی";
            worksheet.Cells[row, 9].Value = "وضعیت";

            StyleHeaderRow(worksheet.Cells[row, 1, row, 9]);
            row++;

            // Order data
            foreach (dynamic order in orders)
            {
                worksheet.Cells[row, 1].Value = order.id?.ToString();
                worksheet.Cells[row, 2].Value = DateTime.Parse(order.createdAt.ToString()).ToString("yyyy/MM/dd HH:mm");
                worksheet.Cells[row, 3].Value = order.customerName?.ToString();
                worksheet.Cells[row, 4].Value = order.fromCurrency?.ToString();
                worksheet.Cells[row, 5].Value = decimal.Parse(order.amount?.ToString() ?? "0");
                worksheet.Cells[row, 6].Value = order.toCurrency?.ToString();
                worksheet.Cells[row, 7].Value = decimal.Parse(order.rate?.ToString() ?? "0");
                worksheet.Cells[row, 8].Value = decimal.Parse(order.totalValue?.ToString() ?? "0");
                worksheet.Cells[row, 9].Value = order.status?.ToString();

                // Style data row
                StyleDataRow(worksheet.Cells[row, 1, row, 9]);
                
                // Format currency columns
                worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0";
                worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0";
                
                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            return package.GetAsByteArray();
        }

        private void StyleHeaderCell(ExcelRange range, int fontSize, bool bold)
        {
            range.Style.Font.Size = fontSize;
            range.Style.Font.Bold = bold;
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
            range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        private void StyleHeaderRow(ExcelRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            
            foreach (var cell in range)
            {
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }
        }

        private void StyleDataRow(ExcelRange range)
        {
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            
            foreach (var cell in range)
            {
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }
        }

        private void StyleInfoCell(ExcelRange cell)
        {
            cell.Style.Font.Bold = true;
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }

        private string GetTransactionTypeText(string? type)
        {
            return type switch
            {
                "Buy" => "خرید",
                "Sell" => "فروش",
                "Document" => "دریافت",
                "DocumentDebit" => "پرداخت",
                "InitialBalance" => "موجودی اولیه",
                "ManualAdjustment" => "تعدیل دستی",
                _ => "تراکنش"
            };
        }

        private void StyleTransactionTypeCell(ExcelRange cell, string? type)
        {
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            
            switch (type)
            {
                case "Buy":
                    // Light green for Buy (خرید)
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
                    break;
                case "Sell":
                    // Light coral for Sell (فروش)
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightCoral);
                    break;
                case "Document":
                    // Light blue for Document (دریافت)
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
                    break;
                case "DocumentDebit":
                    // Light yellow for DocumentDebit (پرداخت)
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                    break;
                case "InitialBalance":
                    // Light cyan for InitialBalance (موجودی اولیه)
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightCyan);
                    break;
                case "ManualAdjustment":
                    // Light pink for ManualAdjustment (تعدیل دستی)
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightPink);
                    break;
                default:
                    // Light gray for other transaction types
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    break;
            }
            
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }
    }
}