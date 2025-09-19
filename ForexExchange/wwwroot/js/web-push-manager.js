/**
 * Web Push Notification Manager
 * مدیر اعلان‌های فشاری وب
 * 
 * This class handles:
 * - Service Worker registration
 * - Push subscription management
 * - Notification permission handling
 * - Integration with existing SignalR notifications
 */
class WebPushManager {
    constructor() {
        this.isSupported = 'serviceWorker' in navigator && 'PushManager' in window;
        this.subscription = null;
        this.publicKey = null; // Will be set from server
        this.isSubscribed = false;
        this.serviceWorkerRegistration = null;
        
        if (this.isSupported) {
            this.init();
        } else {
        }
    }

    /**
     * Initialize push notification system
     * راه‌اندازی سیستم اعلان‌های فشاری
     */
    async init() {
        try {
            await this.registerServiceWorker();
            await this.getPublicKey();
            await this.checkExistingSubscription();
            this.setupEventListeners();
            
        } catch (error) {
        }
    }

    /**
     * Register service worker
     * ثبت Service Worker
     */
    async registerServiceWorker() {
        try {
            this.serviceWorkerRegistration = await navigator.serviceWorker.register('/sw.js', {
                scope: '/'
            });


            // Handle service worker updates
            this.serviceWorkerRegistration.addEventListener('updatefound', () => {
                const newWorker = this.serviceWorkerRegistration.installing;
                if (newWorker) {
                    newWorker.addEventListener('statechange', () => {
                        if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                            this.showUpdateAvailable();
                        }
                    });
                }
            });

        } catch (error) {
            throw error;
        }
    }

    /**
     * Get VAPID public key from server
     * دریافت کلید عمومی VAPID از سرور
     */
    async getPublicKey() {
        try {
            const response = await fetch('/api/push/publickey');
            if (response.ok) {
                const data = await response.json();
                this.publicKey = data.publicKey;
            } else {
                // Use default public key for development
                this.publicKey = 'YOUR_VAPID_PUBLIC_KEY_HERE';
            }
        } catch (error) {
            // Fallback to default key
            this.publicKey = 'YOUR_VAPID_PUBLIC_KEY_HERE';
        }
    }

    /**
     * Check for existing push subscription
     * بررسی وجود اشتراک فشاری موجود
     */
    async checkExistingSubscription() {
        try {
            this.subscription = await this.serviceWorkerRegistration.pushManager.getSubscription();
            this.isSubscribed = this.subscription !== null;
            
            if (this.isSubscribed) {
                // Only sync with server silently, don't show success message
                await this.syncSubscriptionWithServer(this.subscription);
            }
        } catch (error) {
        }
    }

    /**
     * Request notification permission and subscribe
     * درخواست مجوز اعلان و اشتراک
     */
    async requestPermissionAndSubscribe() {
        try {
            // Request notification permission
            const permission = await Notification.requestPermission();
            
            if (permission === 'granted') {
                await this.subscribeToPush();
                return true;
            } else if (permission === 'denied') {
                this.showPermissionDeniedMessage();
                return false;
            } else {
                return false;
            }
        } catch (error) {
            return false;
        }
    }

    /**
     * Subscribe to push notifications
     * اشتراک در اعلان‌های فشاری
     */
    async subscribeToPush() {
        try {
            const applicationServerKey = this.urlBase64ToUint8Array(this.publicKey);
            
            this.subscription = await this.serviceWorkerRegistration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: applicationServerKey
            });

            this.isSubscribed = true;

            // Send subscription to server
            await this.sendSubscriptionToServer(this.subscription);
            
            // Show success message
            this.showSubscriptionSuccess();

        } catch (error) {
            this.showSubscriptionError();
        }
    }

    /**
     * Unsubscribe from push notifications
     * لغو اشتراک از اعلان‌های فشاری
     */
    async unsubscribeFromPush() {
        try {
            if (this.subscription) {
                await this.subscription.unsubscribe();
                await this.removeSubscriptionFromServer();
                
                this.subscription = null;
                this.isSubscribed = false;
                
                this.showUnsubscriptionSuccess();
            }
        } catch (error) {
        }
    }

    /**
     * Send subscription to server
     * ارسال اشتراک به سرور
     */
    async sendSubscriptionToServer(subscription, showSuccessMessage = true) {
        try {
            const response = await fetch('/api/push/subscribe', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                credentials: 'include', // Include authentication cookies
                body: JSON.stringify({
                    subscription: subscription,
                    userAgent: navigator.userAgent,
                    timestamp: new Date().toISOString()
                })
            });

            if (!response.ok) {
                let errorMessage = 'Failed to send subscription to server';
                try {
                    const errorData = await response.json();
                    errorMessage = errorData.error || errorData.message || errorMessage;
                } catch (e) {
                    errorMessage = `HTTP ${response.status}: ${response.statusText}`;
                }
                this.showNotificationMessage(`❌ خطا در ثبت اشتراک: ${errorMessage}`, 'error');
                throw new Error(errorMessage);
            }

            const result = await response.json();
            
            // Only show success message if explicitly requested (not for silent syncs)
            if (showSuccessMessage) {
                this.showNotificationMessage('✅ اشتراک اعلان‌ها با موفقیت ثبت شد', 'success');
            }
        } catch (error) {
            this.showNotificationMessage(`❌ خطا در ارسال اشتراک: ${error.message}`, 'error');
            throw error;
        }
    }

    /**
     * Remove subscription from server
     * حذف اشتراک از سرور
     */
    async removeSubscriptionFromServer() {
        try {
            const response = await fetch('/api/push/unsubscribe', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                credentials: 'include', // Include authentication cookies
                body: JSON.stringify({
                    endpoint: this.subscription.endpoint
                })
            });

            if (!response.ok) {
                throw new Error('Failed to remove subscription from server');
            }

        } catch (error) {
        }
    }

    /**
     * Sync subscription with server silently (no success message)
     * همگام‌سازی بی‌صدا اشتراک با سرور
     */
    async syncSubscriptionWithServer(subscription) {
        try {
            const response = await fetch('/api/push/subscribe', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                credentials: 'include', // Include authentication cookies
                body: JSON.stringify({
                    subscription: {
                        endpoint: subscription.endpoint,
                        keys: {
                            p256dh: btoa(String.fromCharCode.apply(null, new Uint8Array(subscription.getKey('p256dh')))),
                            auth: btoa(String.fromCharCode.apply(null, new Uint8Array(subscription.getKey('auth'))))
                        }
                    }
                })
            });

            if (!response.ok) {
                let errorMessage = 'Failed to sync subscription with server';
                try {
                    const errorData = await response.json();
                    errorMessage = errorData.error || errorData.message || errorMessage;
                } catch (e) {
                    errorMessage = `HTTP ${response.status}: ${response.statusText}`;
                }
                throw new Error(errorMessage);
            }

            const result = await response.json();
            // Note: No success message shown for silent sync
        } catch (error) {
            throw error;
        }
    }

    /**
     * Setup event listeners
     * راه‌اندازی شنوندگان رویداد
     */
    setupEventListeners() {
        // Listen for messages from service worker
        navigator.serviceWorker.addEventListener('message', event => {
            if (event.data && event.data.type === 'NOTIFICATION_CLICK') {
                // Handle notification click navigation
                window.location.href = event.data.url;
            }
        });

        // Handle page visibility changes
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden && this.isSubscribed) {
                // Page became visible, sync subscription status
                this.syncSubscriptionStatus();
            }
        });
    }

    /**
     * Sync subscription status with server
     * همگام‌سازی وضعیت اشتراک با سرور
     */
    async syncSubscriptionStatus() {
        try {
            if (this.subscription) {
                await this.sendSubscriptionToServer(this.subscription, false); // Don't show success message for sync
            }
        } catch (error) {
        }
    }

    /**
     * Convert VAPID key to Uint8Array
     * تبدیل کلید VAPID به Uint8Array
     */
    urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding)
            .replace(/-/g, '+')
            .replace(/_/g, '/');

        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);

        for (let i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    }

    /**
     * Show notification permission setup UI
     * نمایش رابط کاربری تنظیم مجوز اعلان
     */
    showNotificationSetup() {
        Swal.fire({
            title: 'اعلان‌های فشاری',
            html: `
                <div class="text-right" style="direction: rtl;">
                    <p>آیا می‌خواهید اعلان‌های مهم سیستم را حتی زمانی که مرورگر بسته است، دریافت کنید؟</p>
                    <div class="alert alert-info mt-3">
                        <small>
                            <i class="fas fa-info-circle"></i>
                            شما می‌توانید این تنظیم را در هر زمان تغییر دهید
                        </small>
                    </div>
                </div>
            `,
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'فعال کردن اعلان‌ها',
            cancelButtonText: 'بعداً',
            reverseButtons: true,
            customClass: {
                popup: 'rtl-swal-popup',
                title: 'rtl-swal-title',
                htmlContainer: 'rtl-swal-content'
            }
        }).then((result) => {
            if (result.isConfirmed) {
                this.requestPermissionAndSubscribe();
            }
        });
    }

    /**
     * Show subscription success message
     * نمایش پیام موفقیت اشتراک
     */
    showSubscriptionSuccess() {
        Swal.fire({
            title: 'موفق!',
            text: 'اعلان‌های فشاری با موفقیت فعال شدند',
            icon: 'success',
            timer: 3000,
            timerProgressBar: true,
            customClass: {
                popup: 'rtl-swal-popup',
                title: 'rtl-swal-title'
            }
        });
    }

    /**
     * Show subscription error message
     * نمایش پیام خطای اشتراک
     */
    showSubscriptionError() {
        Swal.fire({
            title: 'خطا',
            text: 'متأسفانه امکان فعال‌سازی اعلان‌های فشاری وجود ندارد',
            icon: 'error',
            customClass: {
                popup: 'rtl-swal-popup',
                title: 'rtl-swal-title'
            }
        });
    }

    /**
     * Show unsubscription success message
     * نمایش پیام موفقیت لغو اشتراک
     */
    showUnsubscriptionSuccess() {
        Swal.fire({
            title: 'انجام شد',
            text: 'اعلان‌های فشاری غیرفعال شدند',
            icon: 'info',
            timer: 3000,
            timerProgressBar: true,
            customClass: {
                popup: 'rtl-swal-popup',
                title: 'rtl-swal-title'
            }
        });
    }

    /**
     * Show permission denied message
     * نمایش پیام رد مجوز
     */
    showPermissionDeniedMessage() {
        Swal.fire({
            title: 'مجوز رد شد',
            html: `
                <div class="text-right" style="direction: rtl;">
                    <p>برای فعال‌سازی اعلان‌ها، باید مجوز را در تنظیمات مرورگر فعال کنید:</p>
                    <ol class="text-right mt-3">
                        <li>روی آیکون قفل در نوار آدرس کلیک کنید</li>
                        <li>گزینه "اعلان‌ها" را "مجاز" کنید</li>
                        <li>صفحه را تازه‌سازی کنید</li>
                    </ol>
                </div>
            `,
            icon: 'warning',
            customClass: {
                popup: 'rtl-swal-popup',
                title: 'rtl-swal-title',
                htmlContainer: 'rtl-swal-content'
            }
        });
    }

    /**
     * Show service worker update available
     * نمایش اعلان بروزرسانی Service Worker
     */
    showUpdateAvailable() {
        Swal.fire({
            title: 'بروزرسانی موجود',
            text: 'نسخه جدید سیستم اعلان‌ها آماده است. آیا می‌خواهید به‌روزرسانی کنید؟',
            icon: 'info',
            showCancelButton: true,
            confirmButtonText: 'بروزرسانی',
            cancelButtonText: 'بعداً',
            customClass: {
                popup: 'rtl-swal-popup',
                title: 'rtl-swal-title'
            }
        }).then((result) => {
            if (result.isConfirmed) {
                // Reload page to activate new service worker
                window.location.reload();
            }
        });
    }

    /**
     * Get subscription status
     * دریافت وضعیت اشتراک
     */
    getSubscriptionStatus() {
        return {
            isSupported: this.isSupported,
            isSubscribed: this.isSubscribed,
            permission: Notification.permission,
            subscription: this.subscription
        };
    }

    /**
     * Force re-subscription (for debugging)
     * اجبار اشتراک مجدد (برای دیباگ)
     */
    async forceResubscribe() {
        try {
            
            // First unsubscribe if already subscribed
            if (this.subscription) {
                await this.subscription.unsubscribe();
            }

            // Request permission if needed
            const permission = await Notification.requestPermission();
            if (permission !== 'granted') {
                throw new Error('Notification permission denied');
            }

            // Create new subscription
            this.subscription = await this.serviceWorkerRegistration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: this.urlB64ToUint8Array(this.publicKey)
            });

            this.isSubscribed = true;

            // Send to server
            await this.sendSubscriptionToServer(this.subscription);
            
            this.showNotificationMessage('✅ اشتراک مجدد با موفقیت انجام شد', 'success');
        } catch (error) {
            this.showNotificationMessage(`❌ خطا در اشتراک مجدد: ${error.message}`, 'error');
        }
    }

    /**
     * Check subscription status on server
     * بررسی وضعیت اشتراک در سرور
     */
    async checkServerSubscriptionStatus() {
        try {
            const response = await fetch('/api/push/status', {
                method: 'GET',
                credentials: 'include'
            });

            if (response.ok) {
                const status = await response.json();
                return status;
            } else {
                return null;
            }
        } catch (error) {
            return null;
        }
    }

    /**
     * Test push notification (for development)
     * تست اعلان فشاری (برای توسعه)
     */
    async testPushNotification() {
        if (!this.isSubscribed) {
            try {
                await this.forceResubscribe();
                if (!this.isSubscribed) {
                    this.showNotificationMessage('❌ لطفاً ابتدا اشتراک اعلان‌ها را فعال کنید', 'error');
                    return;
                }
            } catch (error) {
                this.showNotificationMessage('❌ خطا در فعال‌سازی اشتراک اعلان‌ها', 'error');
                return;
            }
        }

        // Check server subscription status first
        const serverStatus = await this.checkServerSubscriptionStatus();
        if (serverStatus && !serverStatus.hasActiveSubscriptions) {
            await this.forceResubscribe();
        }

        try {
            const response = await fetch('/api/push/test', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                credentials: 'include', // Include authentication cookies
                body: JSON.stringify({
                    title: 'تست اعلان فشاری',
                    message: 'این یک اعلان تستی است',
                    type: 'info'
                })
            });

            if (response.ok) {
                const result = await response.json();
                // Show success message to user
                this.showNotificationMessage('✅ اعلان تست با موفقیت ارسال شد', 'success');
            } else {
                // Get error details from response
                let errorMessage = 'Failed to send test push notification';
                try {
                    const errorData = await response.json();
                    errorMessage = errorData.error || errorData.message || errorMessage;
                } catch (e) {
                    errorMessage = `HTTP ${response.status}: ${response.statusText}`;
                }
                this.showNotificationMessage(`❌ خطا در ارسال اعلان تست: ${errorMessage}`, 'error');
            }
        } catch (error) {
            this.showNotificationMessage(`❌ خطا در ارسال اعلان تست: ${error.message}`, 'error');
        }
    }

    /**
     * Show notification message to user
     * نمایش پیام اعلان به کاربر
     */
    showNotificationMessage(message, type = 'info') {
        // Try to use existing notification system (like toastr, sweetalert, etc.)
        if (typeof toastr !== 'undefined') {
            toastr[type](message);
        } else if (typeof Swal !== 'undefined') {
            Swal.fire({
                text: message,
                icon: type === 'error' ? 'error' : type === 'success' ? 'success' : 'info',
                timer: 3000,
                showConfirmButton: false
            });
        } else {
            // Fallback to browser alert
            alert(message);
        }
    }
}

// Initialize Web Push Manager when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.webPushManager = new WebPushManager();
    
    // Add debugging functions to global scope
    window.debugPushNotifications = {
        forceResubscribe: () => window.webPushManager?.forceResubscribe(),
        checkStatus: () => window.webPushManager?.checkServerSubscriptionStatus(),
        testNotification: () => window.webPushManager?.testPushNotification(),
        getSubscription: () => {
            return window.webPushManager?.subscription;
        }
    };
});

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = WebPushManager;
}
