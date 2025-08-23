# Currencies & Rates

## Currency
- Fields: Code, PersianName, Symbol, IsActive, IsBaseCurrency (IRR), DisplayOrder.
- Management: Admin-only; no delete; toggle Active; IRR cannot be deactivated as base.

## Exchange Rates
- Direct: From→To.
- Reverse: computed as 1 / (To→From).
- Cross: via IRR when both legs available.

## Seeding
- IRR as sole base; USD, EUR, AED, OMR, TRY pre-seeded.

## UI Display
- Use PersianName/Code from DB across Orders/Transactions/Reports.
