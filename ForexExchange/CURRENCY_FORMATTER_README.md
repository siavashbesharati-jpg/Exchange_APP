# Currency Amount Formatter - Documentation

## Overview
The Currency Amount Formatter is a comprehensive JavaScript solution that provides real-time number formatting with Persian text conversion for all amount input fields in the ForexExchange application.

## Features

### 🔢 Real-time Number Formatting
- Automatically adds thousand separators (commas) to numbers
- Updates as the user types with debounced processing
- Maintains clean numeric values for form submission

### 🇮🇷 Persian Text Conversion
- Converts numbers to Persian words in real-time
- Supports decimal numbers with proper Persian representation
- Handles negative numbers with "منفی" prefix

### 💡 Interactive Tooltips
- Beautiful tooltips showing formatted numbers and Persian text
- Appears on focus, hover, or when typing
- Responsive design that works on all screen sizes
- Smooth animations and transitions

### 📱 Smart Detection
- Automatically detects amount input fields based on multiple criteria
- Works with dynamically added form fields
- No manual setup required

## Automatic Detection Criteria

The formatter automatically applies to input fields that match any of these criteria:

1. **Type and Step**: `input[type="number"][step="0.01"]`
2. **CSS Class**: `input.currency-amount`
3. **Name Attribute**: Contains "amount" or "Amount"
4. **ID Attribute**: Contains "amount" or "Amount"
5. **Placeholder**: Contains "مقدار" or "مبلغ"

## Examples

### Example 1: Basic Amount Input
```html
<input type="number" step="0.01" class="form-control" name="amount" placeholder="مبلغ را وارد کنید" />
```

### Example 2: Currency Amount Input
```html
<input type="number" step="0.01" class="form-control currency-amount" name="ib_amount" placeholder="مقدار (منفی یا مثبت)" />
```

### Example 3: Custom Amount Input
```html
<input type="number" class="form-control" id="customerAmount" placeholder="مقدار مورد نظر" />
```

## User Experience

### What Users See:
1. **Input Field**: User types `545000`
2. **Tooltip Shows**:
   - 📊 **545,000** (formatted with commas)
   - 📝 **پانصد و چهل و پنج هزار** (Persian text)
   - ℹ️ **معادل 0.55 میلیون** (for large numbers)

### Interactive Features:
- **Focus**: Tooltip appears when input gets focus
- **Typing**: Real-time updates as user types
- **Hover**: Tooltip shows on mouse hover (if input has value)
- **Mobile**: Touch-friendly with responsive design

## Technical Implementation

### Files Structure:
```
wwwroot/
├── js/
│   ├── persian-number-converter.js    # Core number conversion logic
│   ├── currency-amount-formatter.js   # Main formatter with UI
│   └── currency-form-helper.js        # Form submission handling
└── css/
    └── currency-formatter.css          # Styling and animations
```

### Browser Support:
- Modern browsers (Chrome 60+, Firefox 60+, Safari 12+, Edge 79+)
- Mobile browsers (iOS Safari, Chrome Mobile)
- RTL (Right-to-Left) text support

## Form Submission

The formatter ensures clean numeric values are submitted:

### Before Submission:
- Input shows: `545,000` (with formatting)
- Form receives: `545000` (clean number)

### AJAX Support:
- Works with jQuery form serialization
- Compatible with Fetch API
- Handles FormData automatically

## Customization

### CSS Classes for Styling:
```css
.currency-tooltip          /* Tooltip container */
.currency-formatter-input  /* Formatted input fields */
```

### Disable for Specific Inputs:
```html
<input type="number" step="0.01" data-no-currency-format="true" />
```

## Performance

### Optimizations:
- **Debounced Processing**: Prevents excessive calculations
- **Mutation Observer**: Handles dynamic content efficiently  
- **Memory Management**: Automatic cleanup of resources
- **Lazy Loading**: Only processes when needed

### Memory Usage:
- Minimal impact on page performance
- Automatic cleanup when elements are removed
- Efficient tooltip reuse

## Accessibility

### Features:
- **Screen Reader Support**: Proper ARIA attributes
- **Keyboard Navigation**: Full keyboard accessibility
- **High Contrast**: Supports high contrast mode
- **Reduced Motion**: Respects user motion preferences

### Standards Compliance:
- WCAG 2.1 AA compliant
- Semantic HTML structure
- Proper focus management

## Troubleshooting

### Common Issues:

1. **Formatter Not Working**:
   - Check if input matches detection criteria
   - Verify scripts are loaded in correct order
   - Check browser console for errors

2. **Tooltip Not Showing**:
   - Ensure input has a value
   - Check CSS z-index conflicts
   - Verify tooltip positioning

3. **Form Submission Issues**:
   - Check if currency-form-helper.js is loaded
   - Verify form handling in server-side code
   - Test with different browsers

### Debug Mode:
```javascript
// Enable debug logging
window.currencyFormatter.debug = true;
```

## Integration with Existing Code

### No Changes Required:
- Existing input fields automatically work
- No modification to server-side code needed
- Compatible with existing validation

### Optional Enhancements:
```html
<!-- Add CSS class for better detection -->
<input class="form-control currency-amount" ... />

<!-- Add helpful placeholder -->
<input placeholder="مقدار (تومان)" ... />
```

## Examples in Action

### Order Creation:
- Amount input automatically formats numbers
- Shows Persian text for verification
- Maintains precision for calculations

### Customer Balance:
- Initial balance inputs format correctly
- Supports negative values with proper Persian text
- Works with dynamically added rows

### Bank Accounts:
- Account balance formatting
- Real-time feedback during entry
- Consistent experience across forms

## Future Enhancements

### Planned Features:
- Currency-specific formatting rules
- Voice input support
- Advanced mathematical operations
- Multi-language support

### API for Developers:
```javascript
// Manual formatting
const formatted = window.currencyFormatter.format(545000);

// Get clean value
const clean = window.currencyFormatter.getCleanValue(inputElement);

// Convert to Persian
const persian = window.PersianNumberConverter.convertToPersianWords(545000);
```

## Conclusion

The Currency Amount Formatter provides a seamless, user-friendly experience for number input across the entire ForexExchange application. It requires no manual setup, works automatically with existing code, and enhances the user experience significantly.

For support or questions, refer to the source code comments or contact the development team.
