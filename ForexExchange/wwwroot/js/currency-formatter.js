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
        
        let result;
        if (isIRR) {
            // IRR: display value as-is with thousand separators (no rounding - backend handles that)
            result = new Intl.NumberFormat('en-US').format(numAmount);
        } else {
            // Non-IRR: NO ROUNDING - preserve exact precision with up to 8 decimal places
            result = new Intl.NumberFormat('en-US', {
                minimumFractionDigits: 0,
                maximumFractionDigits: 8
            }).format(numAmount);
        }

        return result;
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
            // IRR: return value as-is (no rounding - backend handles that)
            return numAmount.toString();
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
