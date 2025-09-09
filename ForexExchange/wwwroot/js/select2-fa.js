/**
 * Select2 Persian (Farsi) Language File
 * This file provides Persian translations for Select2 components
 */

// Register Persian language for Select2
$.fn.select2.amd.define('select2/i18n/fa', [], function () {
    // Farsi (Persian)
    return {
        errorLoading: function () {
            return 'امکان بارگذاری نتایج وجود ندارد.';
        },
        inputTooLong: function (args) {
            var overChars = args.input.length - args.maximum;
            var message = 'لطفاً ' + overChars + ' کاراکتر را حذف نمایید';
            return message;
        },
        inputTooShort: function (args) {
            var remainingChars = args.minimum - args.input.length;
            var message = 'لطفاً تعداد ' + remainingChars + ' کاراکتر یا بیشتر وارد نمایید';
            return message;
        },
        loadingMore: function () {
            return 'در حال بارگذاری نتایج بیشتر...';
        },
        maximumSelected: function (args) {
            var message = 'شما تنها می‌توانید ' + args.maximum + ' آیتم را انتخاب نمایید';
            return message;
        },
        noResults: function () {
            return 'هیچ نتیجه‌ای یافت نشد';
        },
        searching: function () {
            return 'در حال جستجو...';
        },
        removeAllItems: function () {
            return 'همه موارد را حذف کنید';
        }
    };
});
