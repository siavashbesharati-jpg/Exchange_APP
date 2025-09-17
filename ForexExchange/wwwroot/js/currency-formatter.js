/**
 * Universal Currency Formatter for Exchange Application
 * 
 * Provides consistent formatting for all currency amounts with:
 * - Comma separation (thousands separators)
 * - IRR: No decimals (whole numbers only)
 * - Non-IRR: 3 decimal places with proper rounding
 * 
 * Usage: formatCurrency(amount, currencyCode)
 * Example: formatCurrency(1234.567, 'USD') -> "1,234.568"
 * Example: formatCurrency(1234567.89, 'IRR') -> "1,234,568"
 */

window.ForexCurrencyFormatter = (function() {
    'use strict';

    /**
     * Main currency formatting function
     * @param {number} amount - The numeric amount to format
     * @param {string} currencyCode - The currency code (e.g., 'IRR', 'USD', 'EUR')
     * @returns {string} Formatted currency string
     */
    function formatCurrency(amount, currencyCode = '') {
        // Handle edge cases
        if (amount === null || amount === undefined || isNaN(amount)) {
            return '0';
        }

        // Convert to number if string
        const numAmount = parseFloat(amount);
        
        // Check if currency is IRR
        const isIRR = currencyCode && currencyCode.toUpperCase() === 'IRR';
        
        if (isIRR) {
            // IRR: Smart rounding to nearest appropriate unit
            const rounded = roundIRRToNearestUnit(numAmount);
            return new Intl.NumberFormat('en-US').format(rounded);
        } else {
            // Non-IRR: 3 decimal places with proper rounding, remove trailing zeros
            const rounded = Math.round(numAmount * 1000) / 1000; // Round to 3 decimals
            return new Intl.NumberFormat('en-US', {
                minimumFractionDigits: 0,
                maximumFractionDigits: 3
            }).format(rounded);
        }
    }

    /**
     * Format currency for display in HTML with currency code
     * @param {number} amount - The numeric amount
     * @param {string} currencyCode - The currency code
     * @returns {string} Formatted string with currency code
     */
    function formatCurrencyWithCode(amount, currencyCode = '') {
        const formattedAmount = formatCurrency(amount, currencyCode);
        return currencyCode ? `${formattedAmount} ${currencyCode}` : formattedAmount;
    }

    /**
     * Format currency for input fields (no trailing zeros for display)
     * @param {number} amount - The numeric amount
     * @param {string} currencyCode - The currency code
     * @returns {string} Formatted string suitable for input fields
     */
    function formatCurrencyForInput(amount, currencyCode = '') {
        if (amount === null || amount === undefined || isNaN(amount)) {
            return '';
        }

        const numAmount = parseFloat(amount);
        const isIRR = currencyCode && currencyCode.toUpperCase() === 'IRR';
        
        if (isIRR) {
            return Math.round(numAmount).toString();
        } else {
            // For inputs, show up to 3 decimals but remove trailing zeros
            const rounded = Math.round(numAmount * 1000) / 1000;
            return rounded.toString();
        }
    }

    /**
     * Parse a formatted currency string back to number
     * @param {string} formattedAmount - The formatted currency string
     * @returns {number} Parsed numeric value
     */
    function parseCurrency(formattedAmount) {
        if (!formattedAmount || typeof formattedAmount !== 'string') {
            return 0;
        }
        
        // Remove commas and spaces, parse as float
        const cleaned = formattedAmount.replace(/[,\s]/g, '');
        const parsed = parseFloat(cleaned);
        return isNaN(parsed) ? 0 : parsed;
    }

    /**
     * Validate if amount is properly formatted for currency type
     * @param {number} amount - The amount to validate
     * @param {string} currencyCode - The currency code
     * @returns {boolean} True if valid
     */
    function isValidCurrencyAmount(amount, currencyCode = '') {
        if (isNaN(amount) || amount === null || amount === undefined) {
            return false;
        }

        const isIRR = currencyCode && currencyCode.toUpperCase() === 'IRR';
        
        if (isIRR) {
            // IRR should be whole numbers
            return Number.isInteger(parseFloat(amount));
        }
        
        return true; // Non-IRR can have decimals
    }

    /**
     * Smart rounding for IRR amounts - always rounds UP (ceiling) to the nearest appropriate unit
     * Automatically detects the best rounding unit (billion, million, thousand, hundred, ten)
     * and always rounds up to the nearest multiple of that unit
     * @param {number} value - The IRR amount to round
     * @returns {number} Rounded amount UP to the nearest appropriate unit
     */
    function roundIRRToNearestUnit(value) {
        // Handle zero
        if (value === 0) return 0;

        // For negative numbers, we need special handling to maintain "rounding up" behavior
        // For negatives, "rounding up" means getting closer to zero (less negative)
        const isNegative = value < 0;
        const absValue = Math.abs(value);

        let roundedValue;

        if (absValue >= 1_000_000_000) { // 1 billion and above
            // Round UP to nearest billion
            const billions = absValue / 1_000_000_000;
            roundedValue = Math.ceil(billions) * 1_000_000_000;
        } else if (absValue >= 1_000_000) { // 1 million to 999 million
            // Round UP to nearest million
            const millions = absValue / 1_000_000;
            roundedValue = Math.ceil(millions) * 1_000_000;
        } else if (absValue >= 1_000) { // 1 thousand to 999 thousand
            // Round UP to nearest thousand
            const thousands = absValue / 1_000;
            roundedValue = Math.ceil(thousands) * 1_000;
        } else if (absValue >= 100) { // 100 to 999
            // Round UP to nearest hundred
            const hundreds = absValue / 100;
            roundedValue = Math.ceil(hundreds) * 100;
        } else if (absValue >= 10) { // 10 to 99
            // Round UP to nearest ten
            const tens = absValue / 10;
            roundedValue = Math.ceil(tens) * 10;
        } else { // Less than 10
            // Round UP to nearest whole number
            roundedValue = Math.ceil(absValue);
        }

        // For negative numbers, we want to round towards zero (less negative)
        // So we apply the negative sign to the ceiling result
        return isNegative ? -roundedValue : roundedValue;
    }

    // Public API
    return {
        format: formatCurrency,
        formatWithCode: formatCurrencyWithCode,
        formatForInput: formatCurrencyForInput,
        parse: parseCurrency,
        isValid: isValidCurrencyAmount,
        
        // Alias for backward compatibility
        formatNumber: formatCurrency
    };
})();

// Global convenience function
window.formatCurrency = window.ForexCurrencyFormatter.format;
window.formatCurrencyWithCode = window.ForexCurrencyFormatter.formatWithCode;
