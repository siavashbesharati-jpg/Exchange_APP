# Business Flow: From Order to Settlement

This document describes the end-to-end flow in the system, aligned with the current code (August 2025).

## Roles and Access

- Admin, Operator:
  - Can create/manage Orders, Receipts, Settlements, and view Reports.
  - Manage Currencies and Exchange Rates.
- Customer:
  - Can view profile and own transactions; cannot create or manage orders.

## Core Entities

- Currency (DB-backed): Code, PersianName, Symbol, IsActive, IsBaseCurrency (IRR), DisplayOrder.
- Order: Buy/Sell, FromCurrencyId, ToCurrencyId, Amount, Rate, Status, FilledAmount, CustomerId.
- Transaction: Links a matched Buy and Sell order; Amount, Rate, Status, TotalInToman, timestamps.
- Receipt: Image data + OCR fields for reference/amount/date; IsVerified flag.

## High-level Flow

1) Prepare system
   - Seed and manage Currencies; ensure IRR is the sole base currency.
   - Maintain active Exchange Rates for pairs. Direct/reverse/cross via IRR supported.
   - Configure Settings: CommissionRate, ExchangeFeeRate, min/max/daily limits.

2) Create order (Admin/Operator)
   - Select Customer, FromCurrency, ToCurrency, Amount.
   - Rate resolution (OrdersController.Create):
     - Direct rate (From→To) if active.
     - Reverse rate (To→From) as 1/reverse.
     - Cross-rate via base IRR when both legs to IRR are available.
   - TotalInToman is set for reporting:
     - If To=IRR: Amount*Rate.
     - If From=IRR: Amount.
     - Else: approximated via USD leg fallback or default.
   - Order saved with Status=Open.

3) Match orders (Admin/Operator)
   - OrdersController.Details shows compatible open/partial orders.
   - OrdersController.Match matches best compatible rates and creates Transaction(s):
     - Uses complementary orders with counter OrderType and compatible rate.
     - Supports partial fills; updates FilledAmount/Status on both orders.
     - Calls CurrencyPoolService.ProcessTransactionAsync to update pool balances.

4) Settlement lifecycle (Admin/Operator)
   - SettlementsController.Index/Details provide queue and per-transaction view.
   - Initiate: moves Transaction to PaymentUploaded, asks buyer to upload receipt.
   - Receipt Upload (ReceiptsController.Upload):
     - Accepts image (JPG/PNG/GIF); optional OCR via IOcrService extracts amount/reference/date.
     - On admin verification (ReceiptsController.Verify), IsVerified=true.
   - Confirm Buyer Payment (SettlementsController.ConfirmBuyerPayment):
     - Requires a verified receipt; Transaction → ReceiptConfirmed.
   - Confirm Seller Payment (SettlementsController.ConfirmSellerPayment):
     - Stores bank reference; stays ReceiptConfirmed until completion.
   - Complete (SettlementsController.Complete):
     - Transaction → Completed; sets CompletedAt; triggers notifications.
   - Fail (SettlementsController.Fail):
     - Rolls back FilledAmount and order statuses; Transaction → Failed.

5) Fees and calculations
   - TransactionSettlementService.CalculateSettlementAsync:
     - GrossAmount = TotalInToman.
     - CommissionRate + ExchangeFeeRate from SettingsService (percent → decimal).
     - NetAmount = Gross - Commission - ExchangeFee.
     - BuyerTotalPayment and SellerNetReceived reported.

6) Reporting (Admin/Operator)
   - ReportsController: Financial, Commission, CustomerActivity, OrderBook.
   - CSV export for transactions.

## State Machine Summaries

- Order.Status: Open → PartiallyFilled → Completed; Cancelled (via Delete action) or stays Open.
- Transaction.Status: Pending → PaymentUploaded → ReceiptConfirmed → Completed; Failed.

## Example Scenarios

### Scenario A: IRR-based Buy USD (full fill)

1. Admin creates Buy order for Customer A: From=IRR, To=USD, Amount=100, Rate=600,000.
2. Another open Sell order exists: From=USD, To=IRR, Rate=≤600,000, Amount≥100.
3. Match creates Transaction with Amount=100, Rate=seller.Rate, Status=Pending.
4. Initiate settlement → PaymentUploaded (await receipt).
5. Buyer uploads receipt, admin verifies → ConfirmBuyerPayment → ReceiptConfirmed.
6. Admin confirms seller payout reference → ConfirmSellerPayment.
7. Complete transaction → Completed; reports include commission/fees.

### Scenario B: Cross-currency EUR→USD (partial with multiple matches)

1. Admin creates Sell order for Customer B: From=EUR, To=USD, Amount=2,000.
2. Two Buy orders exist: 1,500 and 1,000 at compatible/higher rates.
3. First match fills 1,500; order becomes PartiallyFilled.
4. Second match fills remaining 500; order becomes Completed; two Transactions created.
5. Each transaction follows receipt/confirmation/completion flow individually.

### Scenario C: Failure and rollback

1. Transaction is Pending; receipt uploaded but not verifiable.
2. Admin chooses Fail with reason.
3. Service rolls back FilledAmount on orders and resets statuses accordingly; Transaction → Failed.

## Operational Notes

- Only Admin/Operator can create or manage orders. Customer UIs do not expose order creation.
- IRR is the base currency; cross-rates fall back via IRR when direct/reverse not available.
- Currency Management: Admin CRUD (no delete), toggle Active; IRR cannot be deactivated as base.
- Currency Pools track balances and risk; updated on every transaction creation.
