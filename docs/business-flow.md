# Business & Domain Flow

End-to-end flow aligned with current code (Aug 2025).

## Roles & Access
- Admin/Operator: manage Orders, Receipts, Settlements, Reports; manage Currencies and Rates.
- Customer: can view own profile and transactions; cannot create/manage orders.

## Core Entities
- Currency (DB): Code, PersianName, Symbol, IsActive, IsBaseCurrency (IRR), DisplayOrder
- Order: Buy/Sell, FromCurrencyId, ToCurrencyId, Amount, Rate, Status, FilledAmount, CustomerId
- Transaction: links matched Buy/Sell orders; Amount, Rate, Status, TotalInToman
- Receipt: image + OCR fields; IsVerified

## High-level Flow
1) Prepare system: seed/manage currencies; set settings (commission/fee/limits).
2) Create order (Admin/Operator): choose pair and rate (direct/reverse/IRR cross fallback).
3) Matching: best compatible rates; partial fills; pool updates.
4) Settlement lifecycle: upload receipt (OCR optional) → verify → confirm payments → complete/fail.
5) Fees: commission/exchange fee from SettingsService; applied in settlement.
6) Reporting: financial, commission, order book, customer activity; CSV export.

## State Machines
- Order: Open → PartiallyFilled → Completed; Cancelled.
- Transaction: Pending → PaymentUploaded → ReceiptConfirmed → Completed; Failed.

## Example Scenarios
- IRR→USD full fill; EUR↔USD partial multi-match; failure and rollback.
