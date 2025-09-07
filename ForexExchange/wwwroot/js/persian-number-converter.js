/**
 * Persian Number Converter - Convert numbers to Persian words
 * Author: ForexExchange System
 * Version: 1.0.0
 */

class PersianNumberConverter {
    constructor() {
        this.ones = [
            '', 'یک', 'دو', 'سه', 'چهار', 'پنج', 'شش', 'هفت', 'هشت', 'نه',
            'ده', 'یازده', 'دوازده', 'سیزده', 'چهارده', 'پانزده', 'شانزده',
            'هفده', 'هجده', 'نوزده'
        ];

        this.tens = [
            '', '', 'بیست', 'سی', 'چهل', 'پنجاه', 'شصت', 'هفتاد', 'هشتاد', 'نود'
        ];

        this.hundreds = [
            '', 'یکصد', 'دویست', 'سیصد', 'چهارصد', 'پانصد', 'ششصد',
            'هفتصد', 'هشتصد', 'نهصد'
        ];

        this.scale = [
            '', 'هزار', 'میلیون', 'میلیارد', 'بیلیون', 'بیلیارد', 'تریلیون'
        ];
    }

    /**
     * Convert number to Persian words
     * @param {number|string} num - Number to convert
     * @returns {string} Persian words representation
     */
    convertToPersianWords(num) {
        if (num === null || num === undefined || num === '') return '';
        
        const number = parseFloat(num);
        if (isNaN(number)) return '';
        
        if (number === 0) return 'صفر';
        
        // Handle very large numbers (limit for readability)
        if (Math.abs(number) >= 1e15) {
            return 'عدد بسیار بزرگ';
        }
        
        // Handle negative numbers
        let isNegative = false;
        let absNumber = number;
        if (number < 0) {
            isNegative = true;
            absNumber = Math.abs(number);
        }

        // Separate integer and decimal parts
        const parts = absNumber.toString().split('.');
        const integerPart = parseInt(parts[0]);
        const decimalPart = parts[1];

        let result = '';

        // Convert integer part
        if (integerPart > 0) {
            result = this.convertIntegerToPersian(integerPart);
        }

        // Convert decimal part (limit to 2 decimal places for readability)
        if (decimalPart && parseInt(decimalPart) > 0) {
            const limitedDecimal = decimalPart.substring(0, 2);
            const decimalWords = this.convertDecimalToPersian(limitedDecimal);
            if (decimalWords) {
                if (result) {
                    result += ' و ' + decimalWords;
                } else {
                    result = decimalWords;
                }
            }
        }

        // Add negative prefix if needed
        if (isNegative && result) {
            result = 'منفی ' + result;
        }

        return result || 'صفر';
    }

    /**
     * Convert integer part to Persian words
     * @param {number} num - Integer number
     * @returns {string} Persian words
     */
    convertIntegerToPersian(num) {
        if (num === 0) return '';

        let result = '';
        let scaleIndex = 0;

        while (num > 0) {
            const chunk = num % 1000;
            if (chunk !== 0) {
                const chunkWords = this.convertChunkToPersian(chunk);
                const scaleWord = this.scale[scaleIndex];
                
                if (scaleWord) {
                    result = chunkWords + ' ' + scaleWord + (result ? ' و ' + result : '');
                } else {
                    result = chunkWords + (result ? ' و ' + result : '');
                }
            }
            num = Math.floor(num / 1000);
            scaleIndex++;
        }

        return result;
    }

    /**
     * Convert decimal part to Persian words
     * @param {string} decimalStr - Decimal part as string
     * @returns {string} Persian words for decimal
     */
    convertDecimalToPersian(decimalStr) {
        if (!decimalStr || parseInt(decimalStr) === 0) return '';

        // Limit to 3 decimal places for readability
        const limitedDecimal = decimalStr.substring(0, 3);
        const decimalNumber = parseInt(limitedDecimal);

        if (decimalNumber === 0) return '';

        const decimalWords = this.convertIntegerToPersian(decimalNumber);
        
        if (limitedDecimal.length === 1) {
            return decimalWords + ' دهم';
        } else if (limitedDecimal.length === 2) {
            return decimalWords + ' صدم';
        } else if (limitedDecimal.length === 3) {
            return decimalWords + ' هزارم';
        }

        return decimalWords;
    }

    /**
     * Convert a chunk (0-999) to Persian words
     * @param {number} chunk - Number between 0-999
     * @returns {string} Persian words
     */
    convertChunkToPersian(chunk) {
        if (chunk === 0) return '';

        let result = '';
        
        // Hundreds
        const hundred = Math.floor(chunk / 100);
        if (hundred > 0) {
            result += this.hundreds[hundred];
        }

        // Tens and ones
        const remainder = chunk % 100;
        if (remainder > 0) {
            if (result) result += ' و ';
            
            if (remainder < 20) {
                result += this.ones[remainder];
            } else {
                const ten = Math.floor(remainder / 10);
                const one = remainder % 10;
                
                result += this.tens[ten];
                if (one > 0) {
                    result += ' و ' + this.ones[one];
                }
            }
        }

        return result;
    }

    /**
     * Format number with thousand separators (commas)
     * @param {number|string} num - Number to format
     * @returns {string} Formatted number with commas
     */
    addThousandSeparators(num) {
        if (num === null || num === undefined || num === '') return '';
        
        const number = parseFloat(num);
        if (isNaN(number)) return num.toString();

        // Use Intl.NumberFormat for proper formatting
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 0,
            maximumFractionDigits: 8
        }).format(number);
    }

    /**
     * Remove thousand separators from formatted number
     * @param {string} formattedNum - Formatted number string
     * @returns {string} Clean number string
     */
    removeThousandSeparators(formattedNum) {
        if (!formattedNum) return '';
        return formattedNum.toString().replace(/,/g, '');
    }
}

// Export for use in other modules
window.PersianNumberConverter = PersianNumberConverter;
