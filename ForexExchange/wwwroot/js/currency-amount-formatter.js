/**
 * Currency Amount Formatter - Real-time number formatting with tooltips
 * Author: ForexExchange System
 * Version: 1.0.0
 * Dependencies: persian-number-converter.js
 */

class CurrencyAmountFormatter {
    constructor() {
        this.converter = new PersianNumberConverter();
        this.tooltips = new Map();
        this.initialized = false;
        this.debounceTimers = new Map();
    }

    /**
     * Initialize the formatter for all amount inputs
     */
    init() {
        if (this.initialized) return;

        // Wait for DOM to be ready
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.setupFormatting());
        } else {
            this.setupFormatting();
        }

        this.initialized = true;
    }

    /**
     * Setup formatting for all relevant input fields
     */
    setupFormatting() {
        // Selectors for amount input fields
        const selectors = [
            'input[type="number"][step="0.01"]',
            'input.currency-amount',
            'input[name*="amount"]',
            'input[name*="Amount"]',
            'input[id*="amount"]',
            'input[id*="Amount"]',
            'input[placeholder*="مقدار"]',
            'input[placeholder*="مبلغ"]'
        ];

        const inputs = document.querySelectorAll(selectors.join(','));
        
        inputs.forEach(input => {
            this.setupInputFormatting(input);
        });

        // Setup observer for dynamically added inputs
        this.setupMutationObserver();
    }

    /**
     * Setup formatting for a specific input element
     * @param {HTMLInputElement} input - Input element to format
     */
    setupInputFormatting(input) {
        if (input.hasAttribute('data-currency-formatter')) return;
        
        input.setAttribute('data-currency-formatter', 'true');
        
        // Create tooltip element
        this.createTooltip(input);
        
        // Add event listeners
        this.addEventListeners(input);
        
        // Format initial value if exists
        if (input.value) {
            this.formatInput(input);
        }
    }

    /**
     * Create tooltip element for an input
     * @param {HTMLInputElement} input - Input element
     */
    createTooltip(input) {
        const tooltipId = 'tooltip-' + Math.random().toString(36).substr(2, 9);
        
        const tooltip = document.createElement('div');
        tooltip.id = tooltipId;
        tooltip.className = 'currency-tooltip';
        tooltip.style.cssText = `
            position: absolute;
            background: #333;
            color: white;
            padding: 8px 12px;
            border-radius: 6px;
            font-size: 13px;
            max-width: 300px;
            word-wrap: break-word;
            z-index: 1060;
            display: none;
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
            font-family: 'Vazirmatn', sans-serif;
            direction: rtl;
            text-align: right;
            line-height: 1.4;
        `;

        // Add arrow
        const arrow = document.createElement('div');
        arrow.style.cssText = `
            position: absolute;
            top: 100%;
            left: 50%;
            transform: translateX(-50%);
            width: 0;
            height: 0;
            border-left: 6px solid transparent;
            border-right: 6px solid transparent;
            border-top: 6px solid #333;
        `;
        tooltip.appendChild(arrow);

        // Smart tooltip placement: append to modal if input is inside one
        const modalParent = input.closest('.modal, .popup, .dropdown-menu');
        if (modalParent) {
            modalParent.appendChild(tooltip);
            // Use higher z-index for modal tooltips
            tooltip.style.zIndex = '1070';
        } else {
            document.body.appendChild(tooltip);
        }
        
        this.tooltips.set(input, tooltip);

        // Position tooltip relative to input
        this.positionTooltip(input, tooltip);
    }

    /**
     * Position tooltip relative to input
     * @param {HTMLInputElement} input - Input element
     * @param {HTMLElement} tooltip - Tooltip element
     */
    positionTooltip(input, tooltip) {
        const rect = input.getBoundingClientRect();
        const modalParent = input.closest('.modal, .popup, .dropdown-menu');
        
        if (modalParent) {
            // Position relative to modal container
            const modalRect = modalParent.getBoundingClientRect();
            tooltip.style.position = 'absolute';
            tooltip.style.left = (rect.left - modalRect.left + (rect.width / 2) - 150) + 'px';
            tooltip.style.top = (rect.top - modalRect.top - tooltip.offsetHeight - 10) + 'px';
        } else {
            // Position relative to viewport (original behavior)
            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
            const scrollLeft = window.pageXOffset || document.documentElement.scrollLeft;
            
            tooltip.style.left = (rect.left + scrollLeft + (rect.width / 2) - 150) + 'px';
            tooltip.style.top = (rect.top + scrollTop - tooltip.offsetHeight - 10) + 'px';
        }
    }

    /**
     * Add event listeners to input
     * @param {HTMLInputElement} input - Input element
     */
    addEventListeners(input) {
        // Input event for real-time formatting
        input.addEventListener('input', (e) => {
            // For rapid typing, use immediate formatting for better UX
            if (e.inputType === 'insertText' || e.inputType === 'deleteContentBackward') {
                this.formatInput(input); // Immediate formatting for typing
            } else {
                this.debounceFormat(input, 50); // Reduced delay for other input types
            }
        });

        // Keyup event for additional responsiveness
        input.addEventListener('keyup', (e) => {
            // Handle special keys immediately
            if (e.key === 'Backspace' || e.key === 'Delete' || e.key === 'Enter') {
                this.formatInput(input);
            }
        });

        // Focus event (removed automatic tooltip show)
        input.addEventListener('focus', () => {
            this.formatInput(input); // Format immediately on focus only
        });

        // Blur event to hide tooltip
        input.addEventListener('blur', () => {
            this.hideTooltip(input);
        });

        // Click event to show/toggle tooltip
        input.addEventListener('click', () => {
            if (input.value) {
                this.formatInput(input); // Ensure latest formatting
                const tooltip = this.tooltips.get(input);
                if (tooltip && tooltip.style.display === 'block') {
                    this.hideTooltip(input);
                } else {
                    this.showTooltip(input);
                }
            }
        });

        // Removed mouse enter/leave events for hover behavior

        // Window resize to reposition tooltips
        window.addEventListener('resize', () => {
            const tooltip = this.tooltips.get(input);
            if (tooltip && tooltip.style.display !== 'none') {
                this.positionTooltip(input, tooltip);
            }
        });

        // Paste event for immediate processing
        input.addEventListener('paste', (e) => {
            // Use setTimeout to allow paste to complete first
            setTimeout(() => {
                this.formatInput(input);
            }, 0);
        });
    }

    /**
     * Debounce formatting to avoid excessive processing
     * @param {HTMLInputElement} input - Input element
     * @param {number} delay - Delay in milliseconds
     */
    debounceFormat(input, delay) {
        const timerId = this.debounceTimers.get(input);
        if (timerId) {
            clearTimeout(timerId);
        }

        const newTimerId = setTimeout(() => {
            this.formatInput(input);
            this.debounceTimers.delete(input);
        }, delay);

        this.debounceTimers.set(input, newTimerId);
    }

    /**
     * Format input value and update tooltip
     * @param {HTMLInputElement} input - Input element
     */
    formatInput(input) {
        const rawValue = input.value;
        if (!rawValue || rawValue.trim() === '') {
            this.hideTooltip(input);
            return;
        }

        // Clean the value
        const cleanValue = this.converter.removeThousandSeparators(rawValue);
        const numericValue = parseFloat(cleanValue);

        if (isNaN(numericValue)) {
            this.hideTooltip(input);
            return;
        }

        // Check if value actually changed to avoid unnecessary processing
        const previousCleanValue = input.getAttribute('data-clean-value');
        if (previousCleanValue === cleanValue) {
            // Value hasn't changed, no need to update
            return;
        }

        // Format with thousand separators
        const formattedNumber = this.converter.addThousandSeparators(numericValue);
        
        // Convert to Persian words (only if number is reasonable size for performance)
        let persianWords = '';
        if (Math.abs(numericValue) < 1e12) { // Limit for performance
            persianWords = this.converter.convertToPersianWords(numericValue);
        } else {
            persianWords = 'عدد بسیار بزرگ';
        }

        // Update tooltip content
        this.updateTooltip(input, formattedNumber, persianWords, numericValue);

        // Store clean value for form submission
        input.setAttribute('data-clean-value', cleanValue);
        
        // Update input placeholder or add visual feedback
        if (formattedNumber !== rawValue) {
            input.setAttribute('data-formatted-value', formattedNumber);
        }

        // Tooltip will only show when user clicks the input (no automatic showing)
    }

    /**
     * Update tooltip content
     * @param {HTMLInputElement} input - Input element
     * @param {string} formattedNumber - Formatted number with commas
     * @param {string} persianWords - Persian words representation
     * @param {number} numericValue - Original numeric value
     */
    updateTooltip(input, formattedNumber, persianWords, numericValue) {
        const tooltip = this.tooltips.get(input);
        if (!tooltip) return;

        let content = '';
        
        // Add close button at the top
        content += `<div style="position: absolute; top: 6px; left: 4px; cursor: pointer; color: #ffffff; opacity: 0.7; font-size: 12px; line-height: 1; padding: 4px 4px 6px 4px; border-radius: 2px; z-index: 10;" 
                    onmouseover="this.style.opacity='1'; this.style.backgroundColor='rgba(255,255,255,0.2)'" 
                    onmouseout="this.style.opacity='0.7'; this.style.backgroundColor='transparent'"
                    onclick="this.closest('.currency-tooltip').style.display='none'">
            <i class="fas fa-times"></i>
        </div>`;
        
        // Add formatted number (with top padding to avoid close button)
        // Show for any valid number including zero and small numbers
        if (formattedNumber) {
            content += `<div style="color: #4CAF50; font-weight: bold; margin-bottom: 6px; padding: 20px 20px 4px 0; border-bottom: 1px solid rgba(255,255,255,0.2);">
                <span style="font-size: 14px;">${formattedNumber}</span>
            </div>`;
        }

        // Add Persian words (with right padding to avoid close button)
        if (persianWords) {
            content += `<div style="color: #FFE082; font-size: 12px; line-height: 1.5; padding: 4px 20px 4px 0;">
                <span>${persianWords}</span>
            </div>`;
        }

        // Keep the arrow element
        const arrow = tooltip.querySelector('div:last-child');
        tooltip.innerHTML = content;
        if (arrow && arrow.style.position === 'absolute') {
            tooltip.appendChild(arrow);
        } else {
            // Recreate arrow if it doesn't exist
            const newArrow = document.createElement('div');
            newArrow.style.cssText = `
                position: absolute;
                top: 100%;
                left: 50%;
                transform: translateX(-50%);
                width: 0;
                height: 0;
                border-left: 6px solid transparent;
                border-right: 6px solid transparent;
                border-top: 6px solid #333;
            `;
            tooltip.appendChild(newArrow);
        }
    }

    /**
     * Show tooltip for an input
     * @param {HTMLInputElement} input - Input element
     */
    showTooltip(input) {
        const tooltip = this.tooltips.get(input);
        if (!tooltip || !input.value) return;

        this.positionTooltip(input, tooltip);
        tooltip.style.display = 'block';
        
        // Faster fade-in animation
        tooltip.style.opacity = '0';
        tooltip.style.transform = 'translateY(5px)'; // Reduced movement
        tooltip.style.transition = 'opacity 0.15s ease, transform 0.15s ease'; // Faster transition
        
        // Use requestAnimationFrame for smoother animation
        requestAnimationFrame(() => {
            tooltip.style.opacity = '1';
            tooltip.style.transform = 'translateY(0)';
        });
    }

    /**
     * Hide tooltip for an input
     * @param {HTMLInputElement} input - Input element
     */
    hideTooltip(input) {
        const tooltip = this.tooltips.get(input);
        if (!tooltip) return;

        tooltip.style.opacity = '0';
        tooltip.style.transform = 'translateY(5px)'; // Reduced movement
        
        setTimeout(() => {
            if (tooltip.style.opacity === '0') { // Only hide if still faded out
                tooltip.style.display = 'none';
            }
        }, 150); // Faster hide delay
    }

    /**
     * Setup mutation observer for dynamically added inputs
     */
    setupMutationObserver() {
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        // Check if the added node is an input
                        if (node.tagName === 'INPUT') {
                            this.checkAndSetupInput(node);
                        }
                        
                        // Check for inputs within the added node
                        const inputs = node.querySelectorAll ? node.querySelectorAll('input[type="number"], input.currency-amount') : [];
                        inputs.forEach(input => this.checkAndSetupInput(input));
                    }
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    /**
     * Check and setup input if it matches our criteria
     * @param {HTMLInputElement} input - Input element to check
     */
    checkAndSetupInput(input) {
        if (input.hasAttribute('data-currency-formatter')) return;

        const shouldFormat = (
            input.type === 'number' && input.step === '0.01'
        ) || (
            input.classList.contains('currency-amount')
        ) || (
            input.name && (input.name.includes('amount') || input.name.includes('Amount'))
        ) || (
            input.id && (input.id.includes('amount') || input.id.includes('Amount'))
        ) || (
            input.placeholder && (input.placeholder.includes('مقدار') || input.placeholder.includes('مبلغ'))
        );

        if (shouldFormat) {
            this.setupInputFormatting(input);
        }
    }

    /**
     * Get clean numeric value from formatted input
     * @param {HTMLInputElement} input - Input element
     * @returns {string} Clean numeric value
     */
    getCleanValue(input) {
        return input.getAttribute('data-clean-value') || this.converter.removeThousandSeparators(input.value);
    }

    /**
     * Clean up resources for an input
     * @param {HTMLInputElement} input - Input element
     */
    cleanup(input) {
        const tooltip = this.tooltips.get(input);
        if (tooltip) {
            tooltip.remove();
            this.tooltips.delete(input);
        }

        const timerId = this.debounceTimers.get(input);
        if (timerId) {
            clearTimeout(timerId);
            this.debounceTimers.delete(input);
        }
    }

    /**
     * Cleanup all resources
     */
    destroy() {
        this.tooltips.forEach((tooltip, input) => {
            this.cleanup(input);
        });
        this.initialized = false;
    }
}

// Initialize when DOM is ready
const currencyFormatter = new CurrencyAmountFormatter();

// Auto-initialize
document.addEventListener('DOMContentLoaded', () => {
    currencyFormatter.init();
});

// Export for global access
window.CurrencyAmountFormatter = CurrencyAmountFormatter;
window.currencyFormatter = currencyFormatter;
