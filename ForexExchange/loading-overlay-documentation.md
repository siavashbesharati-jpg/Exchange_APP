# Beautiful Loading Overlay System - Documentation

## 🎨 Reusable Loading Overlay Components

We've successfully created a beautiful, reusable loading overlay system that can be used across all pages in the application.

## 📁 Files Created

### 1. **Partial View**: `Views/Shared/_LoadingOverlay.cshtml`
Contains the HTML structure for the overlay with spinning animation and text placeholders.

### 2. **CSS File**: `wwwroot/css/loading-overlay.css`
Contains all the styling for the beautiful glass overlay with:
- Black glass transparency with backdrop blur
- Triple spinning rings animation
- Smooth fade-in/slide-up animations
- Persian text support

### 3. **JavaScript File**: `wwwroot/js/loading-overlay.js`
Contains utility functions and predefined messages:
- `showLoadingOverlay(text, subtext)`
- `hideLoadingOverlay()`
- `showLoadingWithMessage(messageKey)`
- Predefined messages for common operations

## 🚀 How to Use

### Step 1: Include the Components
Add these to your view:

```html
<!-- Include the overlay HTML -->
@Html.Partial("_LoadingOverlay")

<!-- Include the CSS and JS files -->
@section Scripts {
    <link rel="stylesheet" href="~/css/loading-overlay.css" />
    <script src="~/js/loading-overlay.js"></script>
    
    <script>
        // Your page-specific code
    </script>
}
```

### Step 2: Use in Your AJAX Calls

#### Basic Usage:
```javascript
// Show overlay
showLoadingOverlay('Custom text...', 'Custom subtext...');

// Hide overlay
hideLoadingOverlay();
```

#### Using Predefined Messages:
```javascript
// Show with predefined message
showLoadingWithMessage('SAVING_DOCUMENT');
showLoadingWithMessage('CONFIRMING_DOCUMENT');
showLoadingWithMessage('CREATING_ORDER');

// Hide overlay
hideLoadingOverlay();
```

#### Complete AJAX Example:
```javascript
document.getElementById('submitBtn').addEventListener('click', function() {
    // Show loading overlay
    showLoadingWithMessage('SAVING_DOCUMENT');
    
    // Your AJAX call
    fetch('/api/save', {
        method: 'POST',
        body: formData
    })
    .then(response => {
        hideLoadingOverlay();
        // Handle success
    })
    .catch(error => {
        hideLoadingOverlay();
        // Handle error
    });
});
```

## 🎯 Predefined Messages

The system includes predefined messages for common operations:

### Document Operations:
- `SAVING_DOCUMENT`: "در حال ثبت سند..." / "تأیید تراکنش و بروزرسانی ترازها"
- `CONFIRMING_DOCUMENT`: "در حال تأیید سند..." / "بروزرسانی ترازها و ارسال اعلان‌ها"
- `DELETING_DOCUMENT`: "در حال حذف سند..." / "بازگردانی تأثیرات مالی"

### Order Operations:
- `CREATING_ORDER`: "در حال ثبت معامله..." / "محاسبه ترازها و بروزرسانی داشبورد ارزها"
- `PROCESSING_ORDER`: "در حال پردازش معامله..." / "محاسبه نرخ ارز و بررسی موجودی"

### Customer Operations:
- `SAVING_CUSTOMER`: "در حال ثبت مشتری..." / "بررسی اطلاعات و ایجاد حساب"
- `UPDATING_CUSTOMER`: "در حال بروزرسانی..." / "ذخیره تغییرات اطلاعات مشتری"

### General Operations:
- `LOADING`: "در حال بارگذاری..." / "لطفاً منتظر بمانید"
- `PROCESSING`: "در حال پردازش..." / "انجام عملیات درخواستی"
- `UPLOADING`: "در حال آپلود..." / "ارسال فایل به سرور"
- `GENERATING_REPORT`: "در حال تولید گزارش..." / "جمع‌آوری و پردازش داده‌ها"

## ✨ Features

- **Beautiful Design**: Glass overlay with backdrop blur
- **Smooth Animations**: Fade-in, slide-up, and spinning animations
- **Persian Support**: RTL text with proper Persian messages
- **User Protection**: Prevents page interaction during operations
- **Auto Cleanup**: Automatically restores page state
- **Error Handling**: Graceful error handling with proper cleanup
- **Reusable**: Single implementation used across all pages

## 🔧 Updated Files

The following files have been updated to use the reusable overlay system:

1. **Views/AccountingDocuments/Upload.cshtml** ✅
   - Replaced inline overlay with `@Html.Partial("_LoadingOverlay")`
   - Replaced inline CSS/JS with external file references
   - Uses `SAVING_DOCUMENT` predefined message

2. **Views/AccountingDocuments/Index.cshtml** ✅
   - Replaced inline overlay with `@Html.Partial("_LoadingOverlay")`
   - Replaced inline CSS/JS with external file references
   - Uses `CONFIRMING_DOCUMENT` predefined message

3. **Views/Orders/Create.cshtml** ✅
   - Replaced inline overlay with `@Html.Partial("_LoadingOverlay")`
   - Replaced inline CSS/JS with external file references
   - Uses `CREATING_ORDER` predefined message

## 💡 Benefits

1. **Maintainability**: Single source for overlay styling and behavior
2. **Consistency**: Same look and feel across all pages
3. **Performance**: CSS and JS files are cached by browser
4. **Flexibility**: Easy to add new predefined messages
5. **Scalability**: Can be easily used in new pages
6. **Professional**: Beautiful glass overlay with smooth animations