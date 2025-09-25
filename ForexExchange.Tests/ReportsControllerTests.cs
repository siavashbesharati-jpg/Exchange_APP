using ForexExchange.Controllers;
using ForexExchange.Models;
using ForexExchange.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ForexExchange.Tests
{
    public class ReportsControllerTests
    {
        private readonly Mock<ILogger<ReportsController>> _loggerMock;
        private readonly Mock<IBankAccountFinancialHistoryService> _bankAccountHistoryServiceMock;
        private readonly Mock<IPoolFinancialHistoryService> _poolHistoryServiceMock;
        private readonly ReportsController _controller;

        public ReportsControllerTests()
        {
            _loggerMock = new Mock<ILogger<ReportsController>>();
            _bankAccountHistoryServiceMock = new Mock<IBankAccountFinancialHistoryService>();
            _poolHistoryServiceMock = new Mock<IPoolFinancialHistoryService>();

            _controller = new ReportsController(
                _loggerMock.Object,
                _bankAccountHistoryServiceMock.Object,
                _poolHistoryServiceMock.Object);
        }

        [Fact]
        public async Task PrintBankAccountReport_WithValidData_ReturnsViewResult()
        {
            // Arrange
            var bankAccountId = "test-bank-account-id";
            var timeline = new List<BankAccountTransactionDto>
            {
                new BankAccountTransactionDto
                {
                    Date = "2024-01-01",
                    Time = "10:00:00",
                    TransactionType = "Deposit",
                    Description = "Test deposit",
                    CurrencyCode = "IRR",
                    Amount = 1000000,
                    Balance = 1000000,
                    ReferenceId = "ref-1",
                    CanNavigate = true
                }
            };

            var summary = new BankAccountSummaryDto
            {
                AccountBalances = new Dictionary<string, decimal>
                {
                    { bankAccountId, 1000000 }
                }
            };

            _bankAccountHistoryServiceMock
                .Setup(s => s.GetBankAccountTimelineAsync(bankAccountId, null, null))
                .ReturnsAsync(timeline);

            _bankAccountHistoryServiceMock
                .Setup(s => s.GetBankAccountSummaryAsync(bankAccountId))
                .ReturnsAsync(summary);

            // Act
            var result = await _controller.PrintBankAccountReport(bankAccountId);

            // Assert
            Assert.IsType<ViewResult>(result);
            var viewResult = result as ViewResult;
            Assert.NotNull(viewResult);
            Assert.IsType<FinancialReportViewModel>(viewResult.Model);
        }

        [Fact]
        public async Task PrintBankAccountReport_WithNullTimeline_ReturnsStatusCode500()
        {
            // Arrange
            var bankAccountId = "test-bank-account-id";

            _bankAccountHistoryServiceMock
                .Setup(s => s.GetBankAccountTimelineAsync(bankAccountId, null, null))
                .ReturnsAsync((List<BankAccountTransactionDto>)null);

            _bankAccountHistoryServiceMock
                .Setup(s => s.GetBankAccountSummaryAsync(bankAccountId))
                .ReturnsAsync(new BankAccountSummaryDto());

            // Act
            var result = await _controller.PrintBankAccountReport(bankAccountId);

            // Assert
            Assert.IsType<StatusCodeResult>(result);
            var statusCodeResult = result as StatusCodeResult;
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task PrintBankAccountReport_WithNullSummary_ReturnsStatusCode500()
        {
            // Arrange
            var bankAccountId = "test-bank-account-id";

            _bankAccountHistoryServiceMock
                .Setup(s => s.GetBankAccountTimelineAsync(bankAccountId, null, null))
                .ReturnsAsync(new List<BankAccountTransactionDto>());

            _bankAccountHistoryServiceMock
                .Setup(s => s.GetBankAccountSummaryAsync(bankAccountId))
                .ReturnsAsync((BankAccountSummaryDto)null);

            // Act
            var result = await _controller.PrintBankAccountReport(bankAccountId);

            // Assert
            Assert.IsType<StatusCodeResult>(result);
            var statusCodeResult = result as StatusCodeResult;
            Assert.Equal(500, statusCodeResult.StatusCode);
        }

        [Fact]
        public async Task PrintPoolReport_WithValidData_ReturnsViewResult()
        {
            // Arrange
            var currencyCode = "USD";
            var timeline = new List<PoolTransactionDto>
            {
                new PoolTransactionDto
                {
                    Date = "2024-01-01",
                    Time = "10:00:00",
                    TransactionType = "Deposit",
                    Description = "Test deposit",
                    CurrencyCode = "USD",
                    Amount = 1000,
                    Balance = 1000,
                    ReferenceId = "ref-1",
                    CanNavigate = true
                }
            };

            var summary = new PoolSummaryDto
            {
                CurrencyBalances = new Dictionary<string, decimal>
                {
                    { currencyCode, 1000 }
                }
            };

            _poolHistoryServiceMock
                .Setup(s => s.GetPoolTimelineAsync(currencyCode, null, null))
                .ReturnsAsync(timeline);

            _poolHistoryServiceMock
                .Setup(s => s.GetPoolSummaryAsync(currencyCode))
                .ReturnsAsync(summary);

            // Act
            var result = await _controller.PrintPoolReport(currencyCode);

            // Assert
            Assert.IsType<ViewResult>(result);
            var viewResult = result as ViewResult;
            Assert.NotNull(viewResult);
            Assert.IsType<FinancialReportViewModel>(viewResult.Model);
        }

        [Fact]
        public async Task PrintPoolReport_WithInvalidDate_SkipsTransaction()
        {
            // Arrange
            var currencyCode = "USD";
            var timeline = new List<PoolTransactionDto>
            {
                new PoolTransactionDto
                {
                    Date = "invalid-date",
                    Time = "10:00:00",
                    TransactionType = "Deposit",
                    Description = "Test deposit",
                    CurrencyCode = "USD",
                    Amount = 1000,
                    Balance = 1000,
                    ReferenceId = "ref-1",
                    CanNavigate = true
                },
                new PoolTransactionDto
                {
                    Date = "2024-01-01",
                    Time = "10:00:00",
                    TransactionType = "Deposit",
                    Description = "Valid deposit",
                    CurrencyCode = "USD",
                    Amount = 500,
                    Balance = 1500,
                    ReferenceId = "ref-2",
                    CanNavigate = true
                }
            };

            var summary = new PoolSummaryDto
            {
                CurrencyBalances = new Dictionary<string, decimal>
                {
                    { currencyCode, 1500 }
                }
            };

            _poolHistoryServiceMock
                .Setup(s => s.GetPoolTimelineAsync(currencyCode, null, null))
                .ReturnsAsync(timeline);

            _poolHistoryServiceMock
                .Setup(s => s.GetPoolSummaryAsync(currencyCode))
                .ReturnsAsync(summary);

            // Act
            var result = await _controller.PrintPoolReport(currencyCode);

            // Assert
            Assert.IsType<ViewResult>(result);
            var viewResult = result as ViewResult;
            Assert.NotNull(viewResult);
            var model = viewResult.Model as FinancialReportViewModel;
            Assert.NotNull(model);
            // Should only include the valid transaction, skip the invalid one
            Assert.Single(model.Transactions);
            Assert.Equal("Valid deposit", model.Transactions.First().Description);
        }
    }
}