/**
 * Automatic Currency Display Formatter
 * Automatically finds and formats numbers displayed on the page
 * Author: ForexExchange System
 * Version: 1.0.0
 */

class AutoCurrencyDisplayFormatter {
    constructor() {
        this.processedElements = new Set();
        this.observer = null;
        this.initialized = false;
    }

    /**
     * Initialize the automatic formatter
     */
    init() {
        if (this.initialized) return;

        // Wait for DOM to be ready
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.setupAutoFormatting());
        } else {
            this.setupAutoFormatting();
        }

        this.initialized = true;
    }

    /**
     * Setup automatic formatting for all displayed numbers
     */
    setupAutoFormatting() {
        // Format existing numbers
        this.formatExistingNumbers();

        // Setup observer for dynamic content
        this.setupMutationObserver();

        // Re-format on window load (for dynamically loaded content)
        window.addEventListener('load', () => {
            setTimeout(() => this.formatExistingNumbers(), 500);
        });
    }

    /**
     * Find and format existing numbers on the page
     */
    formatExistingNumbers() {
        // Selectors for elements that might contain currency values
        const selectors = [
            '.balance-amount h4', // Pool widget balances
            '.currency-amount', // Currency amounts in widgets
            '.summary-value', // Summary values
            '.stat-number', // Statistics
            '.balance-display .balance-amount', // Various balance displays
            '.text-success, .text-danger, .text-warning', // Status colored amounts
            'td:contains("$"), td:contains("€"), td:contains("₹")', // Table cells with currency symbols
            '[class*="amount"]', // Any element with "amount" in class name
            '[id*="amount"]', // Any element with "amount" in id
            '[data-currency]', // Elements with currency data attribute
            '.balance-card .balance-amount', // Balance cards
            '.stat-card .text-success, .stat-card .text-danger', // Stat cards
            'h4, h5, h6', // Headers that might contain amounts
            'td', // Table cells
            '.fw-bold', // Bold text that might be amounts
            '.small, .tiny' // Small text amounts
        ];

        // Also find elements by content patterns - scan all text elements
        const allElements = document.querySelectorAll('*');
        
        allElements.forEach(element => {
            if (this.processedElements.has(element)) return;
            
            // Use the centralized skip logic
            if (this.shouldSkipElement(element)) return;
            
            // Only process leaf elements (elements with text content but no element children)
            const hasOnlyTextNodes = Array.from(element.childNodes).every(child => 
                child.nodeType === Node.TEXT_NODE || 
                (child.nodeType === Node.ELEMENT_NODE && child.tagName === 'I' && child.classList.contains('fa'))
            );
            
            if (hasOnlyTextNodes && element.textContent.trim()) {
                this.processElement(element);
            }
        });
    }

    /**
     * Process a single element for number formatting
     * @param {HTMLElement} element - Element to process
     */
    processElement(element) {
        const textContent = element.textContent?.trim();
        if (!textContent || this.processedElements.has(element)) return;

        // Skip elements that should not be formatted
        if (this.shouldSkipElement(element)) return;

        // Pattern to match numbers that might be currency values
        // Matches: 1234567, 1234567.89, 1,234,567.89, but not phone numbers, dates, etc.
        const currencyPattern = /^\s*-?\s*(\d{1,3}(?:,\d{3})*|\d+)(?:\.\d{1,8})?\s*$/;
        
        // More specific patterns for amounts - made more aggressive
        const amountPatterns = [
            /^\s*-?\s*\d{3,}\s*$/, // Numbers with 3+ digits (likely amounts) - reduced from 4+
            /^\s*-?\s*\d{1,3}(?:,\d{3})+(?:\.\d{1,8})?\s*$/, // Already formatted numbers
            /^\s*-?\s*\d+\.\d{2,}\s*$/, // Decimal amounts
            /^\s*-?\s*\d{1,3},\d{3}\s*$/, // Simple comma separated (1,000)
            /^\s*-?\s*\d{6,}\s*$/ // Large numbers (6+ digits)
        ];

        // Check if this looks like a currency amount
        let isLikelyCurrency = false;
        
        // Check parent context for currency indicators
        const parentContext = this.getParentContext(element);
        if (parentContext.isCurrencyContext) {
            isLikelyCurrency = true;
        }
        
        // Check patterns
        for (const pattern of amountPatterns) {
            if (pattern.test(textContent)) {
                isLikelyCurrency = true;
                break;
            }
        }

        // Additional check for large numbers - reduced threshold
        const numericValue = parseFloat(textContent.replace(/,/g, ''));
        if (!isNaN(numericValue) && Math.abs(numericValue) >= 100) { // Reduced from 1000 to 100
            // Additional context check for smaller numbers
            if (Math.abs(numericValue) < 1000) {
                // Only format smaller numbers if in clear currency context
                if (parentContext.isCurrencyContext) {
                    isLikelyCurrency = true;
                }
            } else {
                isLikelyCurrency = true;
            }
        }

        if (isLikelyCurrency) {
            this.formatElementContent(element, numericValue, parentContext.currencyCode);
        }

        this.processedElements.add(element);
    }

    /**
     * Check if an element should be skipped from formatting
     * @param {HTMLElement} element - Element to check
     * @returns {boolean} True if element should be skipped
     */
    shouldSkipElement(element) {
        // Skip input elements (they're handled by the input formatter)
        if (element.tagName === 'INPUT' || element.tagName === 'TEXTAREA') return true;
        
        // Skip script and style elements
        if (element.tagName === 'SCRIPT' || element.tagName === 'STYLE') return true;
        
        /**
         * Skip protected elements (reference numbers, etc.)
         * =====================================================
         * 
         * These attributes and classes are used to protect elements from automatic formatting:
         * - data-no-format: Legacy protection attribute
         * - data-protected: Legacy protection attribute  
         * - data-skip-format: Modern protection attribute
         * - no-format-number: CSS class for styling + protection
         * - protected-reference: CSS class specifically for reference numbers
         * - skip-auto-format: Modern CSS class for protection
         * 
         * Usage: Add any of these to prevent comma formatting on numbers like reference IDs
         * Example: <span data-no-format="true" class="skip-auto-format">654456</span>
         */
        if (element.hasAttribute('data-no-format') || 
            element.hasAttribute('data-protected') || 
            element.hasAttribute('data-skip-format') ||
            element.classList.contains('no-format-number') ||
            element.classList.contains('protected-reference') ||
            element.classList.contains('skip-auto-format')) {
            return true;
        }
        
        // Skip phone number links
        if (element.tagName === 'A' && element.href && element.href.startsWith('tel:')) return true;
        
        // Skip elements inside phone number links
        let parent = element.parentElement;
        while (parent) {
            if (parent.tagName === 'A' && parent.href && parent.href.startsWith('tel:')) return true;
            parent = parent.parentElement;
        }
        
        // Skip elements with phone-related classes or attributes
        const phoneIndicators = ['phone', 'tel', 'mobile', 'contact'];
        const classList = Array.from(element.classList || []);
        const id = element.id || '';
        
        for (const indicator of phoneIndicators) {
            if (classList.some(cls => cls.toLowerCase().includes(indicator)) ||
                id.toLowerCase().includes(indicator)) {
                return true;
            }
        }
        
        return false;
    }

    /**
     * Get context information from parent elements
     * @param {HTMLElement} element - Element to analyze
     * @returns {Object} Context information
     */
    getParentContext(element) {
        let currentElement = element;
        let context = {
            isCurrencyContext: false,
            currencyCode: null
        };

        // Check up to 5 levels up for context
        for (let i = 0; i < 5 && currentElement; i++) {
            const classList = Array.from(currentElement.classList || []);
            const id = currentElement.id || '';
            const dataAttributes = currentElement.dataset || {};

            // Check for currency context indicators
            const currencyIndicators = [
                'currency', 'amount', 'balance', 'price', 'total', 'rate',
                'bought', 'sold', 'debit', 'credit', 'pool', 'wallet'
            ];

            for (const indicator of currencyIndicators) {
                if (classList.some(cls => cls.toLowerCase().includes(indicator)) ||
                    id.toLowerCase().includes(indicator)) {
                    context.isCurrencyContext = true;
                    break;
                }
            }

            // Look for currency code in data attributes or text
            if (dataAttributes.currency) {
                context.currencyCode = dataAttributes.currency;
            }

            // Look for currency codes in nearby text
            const textContent = currentElement.textContent || '';
            const currencyCodePattern = /(IRR|USD|EUR|AED|OMR|TRY|CNY)/i;
            const match = textContent.match(currencyCodePattern);
            if (match) {
                context.currencyCode = match[1].toUpperCase();
            }

            currentElement = currentElement.parentElement;
        }

        return context;
    }

    /**
     * Format the content of an element
     * @param {HTMLElement} element - Element to format
     * @param {number} numericValue - Numeric value
     * @param {string} currencyCode - Currency code if available
     */
    formatElementContent(element, numericValue, currencyCode) {
        if (isNaN(numericValue)) return;

        // Determine decimal places based on currency
        let formattedValue;
        if (currencyCode === 'IRR' || Math.abs(numericValue) >= 1000000 || numericValue % 1 === 0) {
            // No decimal places for IRR, large numbers, or whole numbers
            formattedValue = new Intl.NumberFormat('en-US', {
                minimumFractionDigits: 0,
                maximumFractionDigits: 0
            }).format(numericValue);
        } else {
            // Preserve up to 8 decimal places for other currencies - NO FORCED ROUNDING
            formattedValue = new Intl.NumberFormat('en-US', {
                minimumFractionDigits: 0,
                maximumFractionDigits: 8
            }).format(numericValue);
        }

        // Update the element content
        const originalText = element.textContent;
        const newText = originalText.replace(/^\s*-?\s*[\d,]+(?:\.\d+)?\s*$/, formattedValue);
        
        if (newText !== originalText) {
            element.textContent = newText;
            
            // Add a subtle animation to indicate the change
            element.style.transition = 'color 0.3s ease';
            const originalColor = window.getComputedStyle(element).color;
            element.style.color = '#4CAF50';
            setTimeout(() => {
                element.style.color = originalColor;
            }, 300);
        }
    }

    /**
     * Setup mutation observer for dynamic content
     */
    setupMutationObserver() {
        this.observer = new MutationObserver((mutations) => {
            let needsProcessing = false;
            
            mutations.forEach((mutation) => {
                if (mutation.type === 'childList') {
                    mutation.addedNodes.forEach((node) => {
                        if (node.nodeType === Node.ELEMENT_NODE) {
                            needsProcessing = true;
                        }
                    });
                } else if (mutation.type === 'characterData') {
                    needsProcessing = true;
                }
            });

            if (needsProcessing) {
                // Debounce the processing
                setTimeout(() => this.formatExistingNumbers(), 100);
            }
        });

        this.observer.observe(document.body, {
            childList: true,
            subtree: true,
            characterData: true
        });
    }

    /**
     * Destroy the formatter and clean up
     */
    destroy() {
        if (this.observer) {
            this.observer.disconnect();
        }
        this.processedElements.clear();
        this.initialized = false;
    }
}

// Auto-initialize when script loads
const autoFormatter = new AutoCurrencyDisplayFormatter();
autoFormatter.init();

// Export for manual control
window.AutoCurrencyDisplayFormatter = AutoCurrencyDisplayFormatter;
window.autoFormatter = autoFormatter;
