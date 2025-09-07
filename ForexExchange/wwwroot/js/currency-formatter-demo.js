/**
 * Currency Formatter Demo and Testing Script
 * This script can be run in browser console to test the formatter
 */

(function() {
    'use strict';

    const demo = {
        /**
         * Test the Persian number converter with sample values
         */
        testConverter() {
            console.log('🧪 Testing Persian Number Converter...\n');
            
            const testValues = [
                0, 1, 12, 123, 1234, 12345, 123456, 1234567, 12345678,
                545000, 1000000, 2500000, 10000000,
                123.45, 1000.50, 0.75, -545000
            ];

            testValues.forEach(value => {
                const formatted = window.PersianNumberConverter ? 
                    new window.PersianNumberConverter().addThousandSeparators(value) : 'Converter not loaded';
                const persian = window.PersianNumberConverter ? 
                    new window.PersianNumberConverter().convertToPersianWords(value) : 'Converter not loaded';
                
                console.log(`${value} → ${formatted} → ${persian}`);
            });
        },

        /**
         * Create a test form with various input types
         */
        createTestForm() {
            console.log('🔧 Creating optimized test form...');
            
            // Remove existing test form
            const existingForm = document.getElementById('currency-test-form');
            if (existingForm) {
                existingForm.remove();
            }

            const form = document.createElement('form');
            form.id = 'currency-test-form';
            form.style.cssText = `
                position: fixed;
                top: 20px;
                right: 20px;
                background: white;
                padding: 20px;
                border: 2px solid #007bff;
                border-radius: 8px;
                box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                z-index: 9999;
                width: 350px;
                font-family: 'Vazirmatn', sans-serif;
                direction: rtl;
            `;

            const title = document.createElement('h4');
            title.textContent = '⚡ تست سریع فرمت کننده ارز';
            title.style.cssText = 'margin: 0 0 15px 0; color: #333; text-align: center;';

            const performance = document.createElement('div');
            performance.innerHTML = '<small style="color: #28a745;">✅ بهینه‌سازی شده برای پاسخ فوری</small>';
            performance.style.cssText = 'margin-bottom: 15px; text-align: center; padding: 8px; background: #f8f9fa; border-radius: 4px;';

            const inputs = [
                {
                    label: '⚡ تست سرعت - مقدار با step="0.01"',
                    html: '<input type="number" step="0.01" class="form-control" placeholder="مثال: 545000 - ببینید چقدر سریع!" style="margin: 5px 0; padding: 8px; border: 1px solid #ccc; border-radius: 4px; width: 100%;" />'
                },
                {
                    label: '💰 ورودی با کلاس currency-amount',
                    html: '<input type="number" class="form-control currency-amount" placeholder="مثال: 1234567 - فوری نمایش!" style="margin: 5px 0; padding: 8px; border: 1px solid #ccc; border-radius: 4px; width: 100%;" />'
                },
                {
                    label: '🔥 تست پاسخ فوری - عدد بزرگ',
                    html: '<input type="number" name="amount" class="form-control" placeholder="مثال: 25000000 - بدون تاخیر!" style="margin: 5px 0; padding: 8px; border: 1px solid #ccc; border-radius: 4px; width: 100%;" />'
                }
            ];

            form.appendChild(title);
            form.appendChild(performance);

            inputs.forEach(input => {
                const labelEl = document.createElement('label');
                labelEl.textContent = input.label;
                labelEl.style.cssText = 'display: block; margin: 10px 0 3px 0; font-weight: bold; font-size: 13px; color: #495057;';
                
                const div = document.createElement('div');
                div.innerHTML = input.html;
                
                form.appendChild(labelEl);
                form.appendChild(div);
            });

            // Add performance info
            const perfInfo = document.createElement('div');
            perfInfo.innerHTML = `
                <small style="color: #6c757d; line-height: 1.4;">
                    <strong>بهینه‌سازی‌ها:</strong><br>
                    • پاسخ فوری (50ms → فوری)<br>
                    • انیمیشن سریع‌تر (300ms → 150ms)<br>
                    • عملکرد بهتر برای اعداد بزرگ<br>
                    • پشتیبانی از Paste
                </small>
            `;
            perfInfo.style.cssText = 'margin: 15px 0; padding: 10px; background: #e9ecef; border-radius: 4px; font-size: 11px;';

            // Add close button
            const closeBtn = document.createElement('button');
            closeBtn.type = 'button';
            closeBtn.textContent = 'بستن';
            closeBtn.style.cssText = `
                background: #dc3545;
                color: white;
                border: none;
                padding: 8px 16px;
                border-radius: 4px;
                cursor: pointer;
                margin-top: 15px;
                width: 100%;
            `;
            closeBtn.onclick = () => form.remove();

            form.appendChild(perfInfo);
            form.appendChild(closeBtn);
            document.body.appendChild(form);

            console.log('✅ تست فرم بهینه شده ساخته شد! اعداد را تایپ کنید و سرعت را ببینید.');
        },

        /**
         * Check if all scripts are loaded
         */
        checkStatus() {
            console.log('🔍 Checking Currency Formatter Status...\n');
            
            const checks = [
                {
                    name: 'PersianNumberConverter',
                    status: typeof window.PersianNumberConverter !== 'undefined',
                    description: 'Core number conversion logic'
                },
                {
                    name: 'CurrencyAmountFormatter',
                    status: typeof window.CurrencyAmountFormatter !== 'undefined',
                    description: 'Main formatter class'
                },
                {
                    name: 'currencyFormatter instance',
                    status: typeof window.currencyFormatter !== 'undefined',
                    description: 'Global formatter instance'
                },
                {
                    name: 'Formatter initialized',
                    status: window.currencyFormatter && window.currencyFormatter.initialized,
                    description: 'Formatter setup completed'
                }
            ];

            checks.forEach(check => {
                const status = check.status ? '✅' : '❌';
                console.log(`${status} ${check.name}: ${check.description}`);
            });

            // Count formatted inputs
            const formattedInputs = document.querySelectorAll('input[data-currency-formatter="true"]');
            console.log(`\n📊 Found ${formattedInputs.length} formatted input(s) on page`);

            if (formattedInputs.length > 0) {
                console.log('Formatted inputs:');
                formattedInputs.forEach((input, index) => {
                    console.log(`  ${index + 1}. ${input.tagName}[${input.type}] - ${input.name || input.id || 'unnamed'}`);
                });
            }
        },

        /**
         * Run all tests
         */
        runAllTests() {
            console.clear();
            console.log('🚀 Currency Formatter Demo & Test Suite\n');
            
            this.checkStatus();
            console.log('\n' + '='.repeat(50) + '\n');
            this.testConverter();
            console.log('\n' + '='.repeat(50) + '\n');
            this.createTestForm();
            
            console.log('\n🎯 Demo completed! Check the test form on the right side of the page.');
            console.log('💡 Try typing numbers like: 545000, 1234567, 123.45');
        },

        /**
         * Show help
         */
        help() {
            console.log(`
🔧 Currency Formatter Demo Commands:

demo.runAllTests()     - Run complete test suite
demo.checkStatus()     - Check if formatter is loaded
demo.testConverter()   - Test number conversion
demo.createTestForm()  - Create test inputs
demo.help()           - Show this help

Example usage:
demo.runAllTests();
            `);
        }
    };

    // Make demo available globally
    window.currencyFormatterDemo = demo;

    // Auto-run if called directly
    if (document.readyState === 'complete') {
        console.log('💡 Currency Formatter Demo loaded! Type "currencyFormatterDemo.help()" for commands.');
    } else {
        document.addEventListener('DOMContentLoaded', () => {
            console.log('💡 Currency Formatter Demo loaded! Type "currencyFormatterDemo.help()" for commands.');
        });
    }

})();
