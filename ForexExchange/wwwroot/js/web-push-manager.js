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
            console.warn('Push notifications not supported in this browser');
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
            
            console.log('Web Push Manager initialized successfully');
        } catch (error) {
            console.error('Error initializing Web Push Manager:', error);
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

            console.log('Service Worker registered successfully');

            // Handle service worker updates
            this.serviceWorkerRegistration.addEventListener('updatefound', () => {
                const newWorker = this.serviceWorkerRegistration.installing;
                if (newWorker) {
                    newWorker.addEventListener('statechange', () => {
                        if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                            console.log('New service worker available');
                            this.showUpdateAvailable();
                        }
                    });
                }
            });

        } catch (error) {
            console.error('Service Worker registration failed:', error);
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
                console.warn('Could not get VAPID public key from server');
                // Use default public key for development
                this.publicKey = 'YOUR_VAPID_PUBLIC_KEY_HERE';
            }
        } catch (error) {
            console.error('Error getting public key:', error);
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
                console.log('Existing push subscription found');
                await this.sendSubscriptionToServer(this.subscription);
            }
        } catch (error) {
            console.error('Error checking existing subscription:', error);
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
                console.warn('Notification permission denied');
                this.showPermissionDeniedMessage();
                return false;
            } else {
                console.warn('Notification permission dismissed');
                return false;
            }
        } catch (error) {
            console.error('Error requesting permission:', error);
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
            console.log('Successfully subscribed to push notifications');

            // Send subscription to server
            await this.sendSubscriptionToServer(this.subscription);
            
            // Show success message
            this.showSubscriptionSuccess();

        } catch (error) {
            console.error('Error subscribing to push notifications:', error);
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
                
                console.log('Successfully unsubscribed from push notifications');
                this.showUnsubscriptionSuccess();
            }
        } catch (error) {
            console.error('Error unsubscribing from push notifications:', error);
        }
    }

    /**
     * Send subscription to server
     * ارسال اشتراک به سرور
     */
    async sendSubscriptionToServer(subscription) {
        try {
            const response = await fetch('/api/push/subscribe', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    subscription: subscription,
                    userAgent: navigator.userAgent,
                    timestamp: new Date().toISOString()
                })
            });

            if (!response.ok) {
                throw new Error('Failed to send subscription to server');
            }

            console.log('Subscription sent to server successfully');
        } catch (error) {
            console.error('Error sending subscription to server:', error);
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
                body: JSON.stringify({
                    endpoint: this.subscription.endpoint
                })
            });

            if (!response.ok) {
                throw new Error('Failed to remove subscription from server');
            }

            console.log('Subscription removed from server successfully');
        } catch (error) {
            console.error('Error removing subscription from server:', error);
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
                await this.sendSubscriptionToServer(this.subscription);
            }
        } catch (error) {
            console.error('Error syncing subscription status:', error);
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
     * Test push notification (for development)
     * تست اعلان فشاری (برای توسعه)
     */
    async testPushNotification() {
        if (!this.isSubscribed) {
            console.warn('Not subscribed to push notifications');
            return;
        }

        try {
            const response = await fetch('/api/push/test', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    title: 'تست اعلان فشاری',
                    message: 'این یک اعلان تستی است',
                    type: 'info'
                })
            });

            if (response.ok) {
                console.log('Test push notification sent');
            } else {
                console.error('Failed to send test push notification');
            }
        } catch (error) {
            console.error('Error sending test push notification:', error);
        }
    }
}

// Initialize Web Push Manager when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.webPushManager = new WebPushManager();
});

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = WebPushManager;
}
