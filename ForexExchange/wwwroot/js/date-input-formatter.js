/**
 * ForexExchange Date Input Handler
 * Ensures consistent Day-Month-Year (dd/MM/yyyy) format for all date inputs
 * This script overrides browser's default locale behavior for date inputs
 */

(function() {
    'use strict';

    // Configuration
    const DATE_FORMAT = {
        display: 'dd/MM/yyyy',
        internal: 'yyyy-MM-dd' // HTML5 standard
    };

    /**
     * Formats a date to dd/MM/yyyy display format
     */
    function formatDisplayDate(date) {
        if (!date || !(date instanceof Date)) return '';
        
        const day = String(date.getDate()).padStart(2, '0');
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const year = date.getFullYear();
        
        return `${day}/${month}/${year}`;
    }

    /**
     * Formats a date to yyyy-MM-dd format for HTML5 date inputs
     */
    function formatInputDate(date) {
        if (!date || !(date instanceof Date)) return '';
        
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        
        return `${year}-${month}-${day}`;
    }

    /**
     * Formats a date to yyyy-MM-ddTHH:mm format for HTML5 datetime-local inputs
     */
    function formatInputDateTime(date) {
        if (!date || !(date instanceof Date)) return '';
        
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');
        
        return `${year}-${month}-${day}T${hours}:${minutes}`;
    }

    /**
     * Parses various date formats and returns a Date object
     */
    function parseDisplayDate(dateString) {
        if (!dateString) return null;
        
        // Remove extra spaces and normalize
        dateString = dateString.trim();
        
        // Try different separators
        const separators = ['/', '-', '.'];
        
        for (const sep of separators) {
            if (dateString.includes(sep)) {
                const parts = dateString.split(sep);
                if (parts.length === 3) {
                    // Assume dd/MM/yyyy format (Day-Month-Year)
                    const day = parseInt(parts[0], 10);
                    const month = parseInt(parts[1], 10) - 1; // JS months are 0-based
                    const year = parseInt(parts[2], 10);
                    
                    if (day >= 1 && day <= 31 && month >= 0 && month <= 11 && year > 1900) {
                        const date = new Date(year, month, day);
                        
                        // Validate the date (e.g., February 30th would become March 2nd)
                        if (date.getFullYear() === year && 
                            date.getMonth() === month && 
                            date.getDate() === day) {
                            return date;
                        }
                    }
                }
                break;
            }
        }
        
        return null;
    }

    /**
     * Creates a text input overlay for date inputs to show Day-Month-Year format
     */
    function createDateDisplayOverlay(dateInput) {
        // Create wrapper
        const wrapper = document.createElement('div');
        wrapper.className = 'date-input-wrapper';
        wrapper.style.position = 'relative';
        wrapper.style.display = 'inline-block';
        wrapper.style.width = '100%';

        // Create display input (what user sees)
        const displayInput = document.createElement('input');
        displayInput.type = 'text';
        displayInput.className = dateInput.className;
        displayInput.placeholder = 'dd/MM/yyyy (روز/ماه/سال)';
        displayInput.style.width = '100%';
        
        // Copy attributes
        if (dateInput.id) displayInput.setAttribute('data-for', dateInput.id);
        if (dateInput.required) displayInput.required = true;
        if (dateInput.disabled) displayInput.disabled = true;

        // Hide original date input but keep it functional
        dateInput.style.position = 'absolute';
        dateInput.style.opacity = '0';
        dateInput.style.pointerEvents = 'none';
        dateInput.style.width = '100%';
        dateInput.style.height = '100%';
        dateInput.setAttribute('data-original-type', dateInput.type);

        // Insert wrapper
        dateInput.parentNode.insertBefore(wrapper, dateInput);
        wrapper.appendChild(displayInput);
        wrapper.appendChild(dateInput);

        // Initialize display value
        if (dateInput.value) {
            const date = new Date(dateInput.value);
            if (!isNaN(date.getTime())) {
                displayInput.value = formatDisplayDate(date);
            }
        }

        // Handle display input changes
        displayInput.addEventListener('input', function(e) {
            const dateValue = parseDisplayDate(e.target.value);
            if (dateValue) {
                if (dateInput.type === 'datetime-local') {
                    // Preserve time if it exists
                    const existingDateTime = dateInput.value ? new Date(dateInput.value) : new Date();
                    if (!isNaN(existingDateTime.getTime()) && dateInput.value.includes('T')) {
                        dateValue.setHours(existingDateTime.getHours());
                        dateValue.setMinutes(existingDateTime.getMinutes());
                    }
                    dateInput.value = formatInputDateTime(dateValue);
                } else {
                    dateInput.value = formatInputDate(dateValue);
                }
                
                // Trigger change event on original input
                dateInput.dispatchEvent(new Event('change', { bubbles: true }));
                dateInput.dispatchEvent(new Event('input', { bubbles: true }));
            }
        });

        // Handle display input blur (validation)
        displayInput.addEventListener('blur', function(e) {
            const dateValue = parseDisplayDate(e.target.value);
            if (e.target.value && !dateValue) {
                e.target.setCustomValidity('لطفاً تاریخ را به فرمت صحیح وارد کنید (روز/ماه/سال)');
                e.target.reportValidity();
            } else {
                e.target.setCustomValidity('');
            }
        });

        // Handle original input changes (from code)
        dateInput.addEventListener('change', function(e) {
            if (e.target.value) {
                const date = new Date(e.target.value);
                if (!isNaN(date.getTime())) {
                    displayInput.value = formatDisplayDate(date);
                }
            } else {
                displayInput.value = '';
            }
        });

        return { wrapper, displayInput, originalInput: dateInput };
    }

    /**
     * Handles datetime-local inputs specifically
     */
    function createDateTimeDisplayOverlay(dateTimeInput) {
        const overlay = createDateDisplayOverlay(dateTimeInput);
        
        // Add time input
        const timeWrapper = document.createElement('div');
        timeWrapper.className = 'datetime-wrapper';
        timeWrapper.style.display = 'flex';
        timeWrapper.style.gap = '5px';
        
        const timeInput = document.createElement('input');
        timeInput.type = 'time';
        timeInput.className = 'form-control';
        timeInput.style.width = '120px';
        timeInput.value = '00:00';
        
        // Update display input width
        overlay.displayInput.style.width = 'calc(100% - 130px)';
        overlay.displayInput.placeholder = 'dd/MM/yyyy';
        
        // Wrap both inputs
        overlay.wrapper.removeChild(overlay.displayInput);
        timeWrapper.appendChild(overlay.displayInput);
        timeWrapper.appendChild(timeInput);
        overlay.wrapper.insertBefore(timeWrapper, overlay.originalInput);

        // Initialize time value
        if (dateTimeInput.value && dateTimeInput.value.includes('T')) {
            const date = new Date(dateTimeInput.value);
            if (!isNaN(date.getTime())) {
                const hours = String(date.getHours()).padStart(2, '0');
                const minutes = String(date.getMinutes()).padStart(2, '0');
                timeInput.value = `${hours}:${minutes}`;
            }
        }

        // Handle time changes
        timeInput.addEventListener('change', function(e) {
            updateDateTime();
        });

        // Update the date input handler to also update time
        const originalDateHandler = overlay.displayInput.cloneNode(true);
        overlay.displayInput.addEventListener('input', function(e) {
            updateDateTime();
        });

        function updateDateTime() {
            const dateValue = parseDisplayDate(overlay.displayInput.value);
            const timeValue = timeInput.value;
            
            if (dateValue && timeValue) {
                const [hours, minutes] = timeValue.split(':');
                dateValue.setHours(parseInt(hours, 10));
                dateValue.setMinutes(parseInt(minutes, 10));
                
                dateTimeInput.value = formatInputDateTime(dateValue);
                dateTimeInput.dispatchEvent(new Event('change', { bubbles: true }));
                dateTimeInput.dispatchEvent(new Event('input', { bubbles: true }));
            }
        }

        // Handle original input changes for datetime
        dateTimeInput.addEventListener('change', function(e) {
            if (e.target.value) {
                const date = new Date(e.target.value);
                if (!isNaN(date.getTime())) {
                    overlay.displayInput.value = formatDisplayDate(date);
                    const hours = String(date.getHours()).padStart(2, '0');
                    const minutes = String(date.getMinutes()).padStart(2, '0');
                    timeInput.value = `${hours}:${minutes}`;
                }
            } else {
                overlay.displayInput.value = '';
                timeInput.value = '00:00';
            }
        });

        return overlay;
    }

    /**
     * Initialize date input formatting for all date inputs on the page
     */
    function initializeDateInputs() {
        // Handle date inputs
        const dateInputs = document.querySelectorAll('input[type="date"]');
        dateInputs.forEach(input => {
            if (!input.hasAttribute('data-date-formatted')) {
                createDateDisplayOverlay(input);
                input.setAttribute('data-date-formatted', 'true');
            }
        });

        // Handle datetime-local inputs
        const dateTimeInputs = document.querySelectorAll('input[type="datetime-local"]');
        dateTimeInputs.forEach(input => {
            if (!input.hasAttribute('data-datetime-formatted')) {
                createDateTimeDisplayOverlay(input);
                input.setAttribute('data-datetime-formatted', 'true');
            }
        });
    }

    /**
     * Set default values for date inputs (today's date)
     */
    function setDefaultDates() {
        const today = new Date();
        
        // Set default values for empty date inputs
        document.querySelectorAll('input[type="date"]').forEach(input => {
            if (!input.value && input.id && (input.id.includes('fromDate') || input.id.includes('toDate'))) {
                input.value = formatInputDate(today);
                input.dispatchEvent(new Event('change', { bubbles: true }));
            }
        });
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            initializeDateInputs();
            setDefaultDates();
        });
    } else {
        initializeDateInputs();
        setDefaultDates();
    }

    // Re-initialize for dynamically added content
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.type === 'childList') {
                mutation.addedNodes.forEach(function(node) {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        // Check if the added node contains date inputs
                        const dateInputs = node.querySelectorAll ? 
                            node.querySelectorAll('input[type="date"], input[type="datetime-local"]') : [];
                        
                        if (dateInputs.length > 0) {
                            setTimeout(initializeDateInputs, 100); // Small delay to ensure DOM is ready
                        }
                    }
                });
            }
        });
    });

    observer.observe(document.body, { childList: true, subtree: true });

    // Expose utility functions globally for use in other scripts
    window.ForexDateUtils = {
        formatDisplayDate,
        formatInputDate,
        formatInputDateTime,
        parseDisplayDate,
        initializeDateInputs
    };

})();