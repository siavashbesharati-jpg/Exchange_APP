/**
 * Real-time Admin Notifications with SignalR and SweetAlert2
 * اعلان‌های بلادرنگ ادمین با SignalR و SweetAlert2
 */

class AdminNotificationManager {
    constructor() {
        this.connection = null;
        this.isConnected = false;
        this.notificationQueue = [];
        this.maxQueueSize = 10;
        this.init();
    }

    /**
     * Initialize the notification system
     * راه‌اندازی سیستم اعلان
     */
    init() {
        this.setupSignalR();
        this.setupSweetAlert2();
        this.bindEvents();
    }

    /**
     * Setup SignalR connection
     * راه‌اندازی اتصال SignalR
     */
    setupSignalR() {
        // Check if SignalR is available
        if (typeof signalR === 'undefined') {
            console.warn('SignalR is not loaded. Real-time notifications will not work.');
            return;
        }

        this.connection = new signalR.HubConnectionBuilder()
            .withUrl('/notificationHub', {
                accessTokenFactory: () => this.getAccessToken()
            })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();

        this.setupSignalREvents();
        this.startConnection();
    }

    /**
     * Setup SignalR event handlers
     * راه‌اندازی کنترل‌کننده‌های رویداد SignalR
     */
    setupSignalREvents() {
        this.connection.on('ReceiveNotification', (notification) => {
            this.handleNotification(notification);
        });

        this.connection.onreconnecting(() => {
            console.log('Reconnecting to notification hub...');
            this.showConnectionStatus('در حال اتصال مجدد...', 'warning');
        });

        this.connection.onreconnected(() => {
            console.log('Reconnected to notification hub');
            // Remove the connection established notification
            // this.showConnectionStatus('اتصال برقرار شد', 'success');
        });

        this.connection.onclose(() => {
            console.log('Connection closed');
            this.isConnected = false;
            this.showConnectionStatus('اتصال قطع شد', 'error');
        });
    }

    /**
     * Start SignalR connection
     * شروع اتصال SignalR
     */
    async startConnection() {
        try {
            await this.connection.start();
            this.isConnected = true;
            console.log('Connected to notification hub');
            // Remove the connection established notification
            // this.showConnectionStatus('اتصال برقرار شد', 'success');
        } catch (err) {
            console.error('Error connecting to notification hub:', err);
            this.showConnectionStatus('خطا در اتصال', 'error');
            // Retry connection after 5 seconds
            setTimeout(() => this.startConnection(), 5000);
        }
    }

    /**
     * Setup SweetAlert2 configuration
     * راه‌اندازی تنظیمات SweetAlert2
     */
    setupSweetAlert2() {
        // Configure SweetAlert2 defaults for Persian/RTL
        Swal.mixin({
            confirmButtonText: 'تأیید',
            cancelButtonText: 'لغو',
            customClass: {
                popup: 'rtl-swal-popup',
                title: 'rtl-swal-title',
                content: 'rtl-swal-content',
                confirmButton: 'btn btn-primary',
                cancelButton: 'btn btn-secondary'
            },
            buttonsStyling: false,
            showClass: {
                popup: 'animate__animated animate__fadeInDown'
            },
            hideClass: {
                popup: 'animate__animated animate__fadeOutUp'
            }
        });
    }

    /**
     * Bind additional events
     * اتصال رویدادهای اضافی
     */
    bindEvents() {
        // Handle page visibility changes
        document.addEventListener('visibilitychange', () => {
            if (document.hidden) {
                // Page is hidden, reduce notification frequency if needed
                console.log('Page hidden, notifications paused');
            } else {
                // Page is visible, resume notifications
                console.log('Page visible, notifications resumed');
                this.processNotificationQueue();
            }
        });

        // Handle beforeunload to gracefully disconnect
        window.addEventListener('beforeunload', () => {
            if (this.connection && this.isConnected) {
                this.connection.stop();
            }
        });
    }

    /**
     * Handle incoming notification
     * مدیریت اعلان ورودی
     */
    handleNotification(notification) {
        console.log('Received notification:', notification);

        // Add to queue if page is not visible
        if (document.hidden) {
            this.addToQueue(notification);
            return;
        }

        // Show notification immediately
        this.showNotification(notification);
    }

    /**
     * Show notification using SweetAlert2
     * نمایش اعلان با SweetAlert2
     */
    showNotification(notification) {
        const config = this.getNotificationConfig(notification);

        Swal.fire(config).then((result) => {
            if (result.isConfirmed) {
                // User clicked "بروزسانی" - refresh the page
                this.refreshCurrentPage();
            }
            // If user clicked "بی خیال" or dismissed, do nothing
        });
    }

    /**
     * Get notification configuration based on type
     * دریافت تنظیمات اعلان بر اساس نوع
     */
    getNotificationConfig(notification) {
        const baseConfig = {
            title: notification.title || 'اعلان',
            text: notification.message || '',
            icon: this.getIconForType(notification.type),
            showConfirmButton: true,
            showCancelButton: true,
            confirmButtonText: 'بروزسانی',
            cancelButtonText: 'بی خیال',
            customClass: {
                popup: 'admin-notification-modal rtl-swal-popup',
                title: 'rtl-swal-title',
                content: 'rtl-swal-content',
                confirmButton: 'btn btn-primary',
                cancelButton: 'btn btn-secondary'
            },
            buttonsStyling: false,
            showClass: {
                popup: 'animate__animated animate__fadeInDown'
            },
            hideClass: {
                popup: 'animate__animated animate__fadeOutUp'
            }
        };

        // Add specific configurations based on notification type
        switch (notification.type) {
            case 'success':
                return {
                    ...baseConfig,
                    icon: 'success'
                };

            case 'error':
                return {
                    ...baseConfig,
                    icon: 'error'
                };

            case 'warning':
                return {
                    ...baseConfig,
                    icon: 'warning'
                };

            case 'info':
            default:
                return {
                    ...baseConfig,
                    icon: 'info'
                };
        }
    }

    /**
     * Get icon for notification type
     * دریافت آیکون برای نوع اعلان
     */
    getIconForType(type) {
        switch (type) {
            case 'success': return 'success';
            case 'error': return 'error';
            case 'warning': return 'warning';
            case 'info': return 'info';
            default: return 'info';
        }
    }

    /**
     * Add notification to queue
     * اضافه کردن اعلان به صف
     */
    addToQueue(notification) {
        this.notificationQueue.push({
            ...notification,
            queuedAt: new Date()
        });

        // Keep queue size manageable
        if (this.notificationQueue.length > this.maxQueueSize) {
            this.notificationQueue.shift();
        }
    }

    /**
     * Process notification queue
     * پردازش صف اعلان‌ها
     */
    processNotificationQueue() {
        while (this.notificationQueue.length > 0) {
            const notification = this.notificationQueue.shift();
            this.showNotification(notification);
        }
    }

    /**
     * Show connection status notification
     * نمایش اعلان وضعیت اتصال
     */
    showConnectionStatus(message, type) {
        const statusElement = document.getElementById('connectionStatus');
        const textElement = document.getElementById('connectionText');

        if (statusElement && textElement) {
            // Update text
            textElement.textContent = message;

            // Update styling
            statusElement.className = 'connection-status';

            switch (type) {
                case 'success':
                    statusElement.classList.add('connected');
                    break;
                case 'error':
                    statusElement.classList.add('disconnected');
                    break;
                case 'warning':
                    statusElement.classList.add('connecting');
                    break;
            }

            // Show the indicator
            statusElement.style.display = 'flex';

            // Auto-hide success messages after 3 seconds
            if (type === 'success') {
                setTimeout(() => {
                    statusElement.style.display = 'none';
                }, 3000);
            }
        }

        // Also show SweetAlert for errors - DISABLED to prevent annoying toasts
        /*
        if (type === 'error') {
            Swal.fire({
                title: 'وضعیت اتصال',
                text: message,
                icon: type,
                toast: true,
                position: 'bottom-end',
                showConfirmButton: false,
                timer: 3000,
                timerProgressBar: true
            });
        }
        */
    }

    /**
     * Get access token for SignalR authentication
     * دریافت توکن دسترسی برای احراز هویت SignalR
     */
    getAccessToken() {
        // Try to get token from meta tag or cookie
        const token = document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') ||
                     this.getCookie('XSRF-TOKEN') ||
                     this.getCookie('.AspNetCore.Identity.Application');

        return token || '';
    }

    /**
     * Get cookie value by name
     * دریافت مقدار کوکی بر اساس نام
     */
    getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    }

    /**
     * Join a specific notification group
     * پیوستن به گروه اعلان خاص
     */
    async joinGroup(groupName) {
        if (this.connection && this.isConnected) {
            try {
                await this.connection.invoke('JoinGroup', groupName);
                console.log(`Joined notification group: ${groupName}`);
            } catch (err) {
                console.error(`Error joining group ${groupName}:`, err);
            }
        }
    }

    /**
     * Leave a specific notification group
     * ترک گروه اعلان خاص
     */
    async leaveGroup(groupName) {
        if (this.connection && this.isConnected) {
            try {
                await this.connection.invoke('LeaveGroup', groupName);
                console.log(`Left notification group: ${groupName}`);
            } catch (err) {
                console.error(`Error leaving group ${groupName}:`, err);
            }
        }
    }

    /**
     * Refresh the current page with current routing parameters
     * بروزرسانی صفحه فعلی با پارامترهای مسیریابی فعلی
     */
    refreshCurrentPage() {
        // Get current URL and refresh the page
        window.location.reload();
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    // Initialize notification manager
    window.adminNotificationManager = new AdminNotificationManager();
});

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = AdminNotificationManager;
}
