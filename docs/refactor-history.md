# Refactor History (CurrencyType → DB-backed Currency)

## Overview
Removal of CurrencyType enum; adoption of Currency entity and navigation properties throughout.

## Highlights
- Orders/Transactions: FromCurrency/ToCurrency navigation; CurrencyPair display.
- Views: DB-driven dropdowns and labels; removed hardcoded currency lists.
- Controllers/Services: logic updated to foreign keys and includes.
- Seeding: IRR base + common currencies; base uniqueness enforced.

## Legacy Doc
- See REFACTOR_SUMMARY.md (archived) for the step-by-step plan and remaining items at the time of refactor.
