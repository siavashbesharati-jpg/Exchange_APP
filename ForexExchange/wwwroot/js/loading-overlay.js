/**
 * Beautiful Loading Overlay JavaScript Utilities
 * File: /js/loading-overlay.js
 * 
 * Prerequisites:
 * 1. Include the CSS: <link rel="stylesheet" href="~/css/loading-overlay.css" />
 * 2. Include the partial view: @Html.Partial("_LoadingOverlay")
 * 3. Include this script: <script src="~/js/loading-overlay.js"></script>
 * 
 * Usage:
 * showLoadingOverlay('Custom text...', 'Custom subtext...');
 * hideLoadingOverlay();
 */

/**
 * Show the beautiful loading overlay with custom text
 * @param {string} text - Main loading text (default: 'در حال پردازش...')
 * @param {string} subtext - Secondary text (default: 'لطفاً منتظر بمانید')
 */
function showLoadingOverlay(text = 'در حال پردازش...', subtext = 'لطفاً منتظر بمانید') {
    const overlay = document.getElementById('loadingOverlay');
    
    if (!overlay) {
        console.error('Loading overlay not found! Make sure to include @Html.Partial("_LoadingOverlay") in your view.');
        return;
    }
    
    const loadingText = overlay.querySelector('.loading-text');
    const loadingSubtext = overlay.querySelector('.loading-subtext');
    
    if (loadingText) {
        loadingText.textContent = text;
        // Force immediate visibility - no animation delay
        loadingText.style.opacity = '1';
    }
    
    if (loadingSubtext) {
        loadingSubtext.textContent = subtext;
        // Force immediate visibility - no animation delay
        loadingSubtext.style.opacity = '1';
    }
    
    // Use style.display and opacity to show immediately - no animation delay
    overlay.style.display = 'flex';
    overlay.style.opacity = '1';
    overlay.classList.add('show');
    document.body.style.overflow = 'hidden';
}

/**
 * Hide the loading overlay and restore page scrolling
 */
function hideLoadingOverlay() {
    const overlay = document.getElementById('loadingOverlay');
    
    if (!overlay) {
        console.error('Loading overlay not found! Make sure to include @Html.Partial("_LoadingOverlay") in your view.');
        return;
    }
    
    // Use style.display to ensure it's properly hidden
    overlay.style.display = 'none';
    overlay.classList.remove('show');
    document.body.style.overflow = '';
}

/**
 * Check if the loading overlay is currently visible
 * @returns {boolean} True if overlay is visible, false otherwise
 */
function isLoadingOverlayVisible() {
    const overlay = document.getElementById('loadingOverlay');
    return overlay && overlay.classList.contains('show');
}

/**
 * Predefined loading messages for common operations
 */
const LoadingMessages = {
    // Document operations
    SAVING_DOCUMENT: {
        text: 'در حال ثبت سند...',
        subtext: 'تأیید تراکنش و بروزرسانی ترازها'
    },
    CONFIRMING_DOCUMENT: {
        text: 'در حال تأیید سند...',
        subtext: 'بروزرسانی ترازها و ارسال اعلان‌ها'
    },
    DELETING_DOCUMENT: {
        text: 'در حال حذف سند...',
        subtext: 'بازگردانی تأثیرات مالی'
    },
    
    // Order operations
    CREATING_ORDER: {
        text: 'در حال ثبت معامله...',
        subtext: 'محاسبه ترازها و بروزرسانی داشبورد ارزها'
    },
    PROCESSING_ORDER: {
        text: 'در حال پردازش معامله...',
        subtext: 'محاسبه نرخ ارز و بررسی موجودی'
    },
    
    // Customer operations
    SAVING_CUSTOMER: {
        text: 'در حال ثبت مشتری...',
        subtext: 'بررسی اطلاعات و ایجاد حساب'
    },
    UPDATING_CUSTOMER: {
        text: 'در حال بروزرسانی...',
        subtext: 'ذخیره تغییرات اطلاعات مشتری'
    },
    
    // General operations
    LOADING: {
        text: 'در حال بارگذاری...',
        subtext: 'لطفاً منتظر بمانید'
    },
    PROCESSING: {
        text: 'در حال پردازش...',
        subtext: 'انجام عملیات درخواستی'
    },
    UPLOADING: {
        text: 'در حال آپلود...',
        subtext: 'ارسال فایل به سرور'
    },
    GENERATING_REPORT: {
        text: 'در حال تولید گزارش...',
        subtext: 'جمع‌آوری و پردازش داده‌ها'
    }
};

/**
 * Show loading overlay with predefined message
 * @param {string} messageKey - Key from LoadingMessages object
 * @param {string} customText - Optional custom text to override
 * @param {string} customSubtext - Optional custom subtext to override
 */
function showLoadingWithMessage(messageKey, customText = null, customSubtext = null) {
    const message = LoadingMessages[messageKey];
    
    if (!message) {
        console.warn(`Loading message key '${messageKey}' not found. Using default message.`);
        showLoadingOverlay(customText, customSubtext);
        return;
    }
    
    const text = customText || message.text;
    const subtext = customSubtext || message.subtext;
    
    showLoadingOverlay(text, subtext);
}

// Auto-initialization - ensure overlay is available when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    // Check if overlay exists
    const overlay = document.getElementById('loadingOverlay');
    if (!overlay) {
        console.warn('Loading overlay not found in DOM. Make sure to include @Html.Partial("_LoadingOverlay") in your view.');
    }
});