# Trailing Zero Removal Test Results

## Backend (C#) FormatCurrency Examples:

```csharp
using ForexExchange.Extensions;

// Non-IRR currencies (truncate to 2 decimals, remove trailing zeros)
23.60m.FormatCurrency("USD")  // Result: "23.6"
23.00m.FormatCurrency("EUR")  // Result: "23"
23.06m.FormatCurrency("GBP")  // Result: "23.06"
1234.50m.FormatCurrency("USD") // Result: "1,234.5"
1234.00m.FormatCurrency("USD") // Result: "1,234"

// IRR currency (no decimals)
234000.534m.FormatCurrency("IRR") // Result: "234,000"
23.60m.FormatCurrency("IRR")      // Result: "23"
```

## Frontend (JavaScript) formatCurrency Examples:

```javascript
// Non-IRR currencies (truncate to 2 decimals, remove trailing zeros)
formatCurrency(23.60, 'USD')  // Result: "23.6"
formatCurrency(23.00, 'EUR')  // Result: "23"
formatCurrency(23.06, 'GBP')  // Result: "23.06"
formatCurrency(1234.50, 'USD') // Result: "1,234.5"
formatCurrency(1234.00, 'USD') // Result: "1,234"

// IRR currency (no decimals)
formatCurrency(234000.534, 'IRR') // Result: "234,000"
formatCurrency(23.60, 'IRR')      // Result: "23"
```

## Key Features:
- ✅ No rounding (only truncation)
- ✅ IRR: All decimals dropped
- ✅ Non-IRR: Exactly 2 decimals max (truncated)
- ✅ Trailing zeros removed: 23.60 → 23.6, 23.00 → 23
- ✅ Thousand separators maintained: 1,234.5
- ✅ Unified behavior between backend and frontend

## Implementation Details:
- **Backend**: Uses `TrimEnd('0').TrimEnd('.')` after formatting
- **Frontend**: Uses `result.replace(/\.?0+$/, '')` regex pattern
- Both approaches remove trailing zeros while preserving necessary decimals