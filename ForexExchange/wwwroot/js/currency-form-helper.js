/**
 * Form Submission Helper for Currency Formatter
 * Ensures clean numeric values are submitted
 */

(function() {
    'use strict';

    /**
     * Clean currency values before form submission
     */
    function cleanCurrencyValuesOnSubmit() {
        // Find all forms in the document
        const forms = document.querySelectorAll('form');
        
        forms.forEach(form => {
            form.addEventListener('submit', function(e) {
                // Find all inputs with currency formatting
                const currencyInputs = form.querySelectorAll('input[data-currency-formatter="true"]');
                
                currencyInputs.forEach(input => {
                    const cleanValue = input.getAttribute('data-clean-value');
                    if (cleanValue && window.currencyFormatter) {
                        // Temporarily set clean value for submission
                        const originalValue = input.value;
                        input.value = cleanValue;
                        
                        // Restore formatted value after a short delay (for UX)
                        setTimeout(() => {
                            if (input.value === cleanValue) {
                                input.value = originalValue;
                            }
                        }, 100);
                    }
                });
            });
        });
    }

    /**
     * Handle AJAX form submissions
     */
    function setupAjaxFormHandling() {
        // Override jQuery form serialization if jQuery is available
        if (window.jQuery) {
            const originalSerializeArray = jQuery.fn.serializeArray;
            
            jQuery.fn.serializeArray = function() {
                const result = originalSerializeArray.call(this);
                
                // Find currency formatted inputs in this form
                this.find('input[data-currency-formatter="true"]').each(function() {
                    const cleanValue = this.getAttribute('data-clean-value');
                    if (cleanValue) {
                        // Update the serialized data
                        const fieldName = this.name;
                        const existingField = result.find(item => item.name === fieldName);
                        if (existingField) {
                            existingField.value = cleanValue;
                        }
                    }
                });
                
                return result;
            };
        }

        // Handle fetch API calls
        const originalFetch = window.fetch;
        window.fetch = function(url, options) {
            if (options && options.body instanceof FormData) {
                // Process FormData for currency inputs
                const currencyInputs = document.querySelectorAll('input[data-currency-formatter="true"]');
                currencyInputs.forEach(input => {
                    const cleanValue = input.getAttribute('data-clean-value');
                    if (cleanValue && input.name) {
                        options.body.set(input.name, cleanValue);
                    }
                });
            }
            
            return originalFetch.apply(this, arguments);
        };
    }

    /**
     * Initialize form handling
     */
    function init() {
        // Wait for DOM to be ready
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => {
                cleanCurrencyValuesOnSubmit();
                setupAjaxFormHandling();
            });
        } else {
            cleanCurrencyValuesOnSubmit();
            setupAjaxFormHandling();
        }

        // Handle dynamically added forms
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        if (node.tagName === 'FORM') {
                            setupFormSubmissionHandler(node);
                        }
                        
                        const forms = node.querySelectorAll ? node.querySelectorAll('form') : [];
                        forms.forEach(form => setupFormSubmissionHandler(form));
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
     * Setup submission handler for a specific form
     * @param {HTMLFormElement} form - Form element
     */
    function setupFormSubmissionHandler(form) {
        if (form.hasAttribute('data-currency-submit-handler')) return;
        
        form.setAttribute('data-currency-submit-handler', 'true');
        
        form.addEventListener('submit', function(e) {
            const currencyInputs = form.querySelectorAll('input[data-currency-formatter="true"]');
            
            currencyInputs.forEach(input => {
                const cleanValue = input.getAttribute('data-clean-value');
                if (cleanValue) {
                    input.value = cleanValue;
                }
            });
        });
    }

    // Initialize
    init();

})();
