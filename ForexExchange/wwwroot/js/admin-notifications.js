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
            .withUrl('/notificationHub')
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
            this.showConnectionStatus('اتصال برقرار شد', 'success');
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
            this.showConnectionStatus('اتصال برقرار شد', 'success');
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
            title: `<i class="fas fa-bell me-2 text-primary"></i>${notification.title || 'اعلان'}`,
            html: `<div class="notification-content">
                      <div class="notification-message mb-3">${notification.message || ''}</div>
                      ${notification.data ? this.formatNotificationData(notification.data) : ''}
                      <small class="text-muted"><i class="fas fa-clock me-1"></i>${new Date(notification.timestamp).toLocaleString('fa-IR')}</small>
                   </div>`,
            icon: this.getIconForType(notification.type),
            showConfirmButton: true,
            showCancelButton: true,
            confirmButtonText: '<i class="fas fa-sync-alt me-2"></i>بروزرسانی صفحه',
            cancelButtonText: '<i class="fas fa-times me-2"></i>ادامه کار',
            customClass: {
                popup: 'admin-notification-modal rtl-swal-popup shadow-lg',
                title: 'rtl-swal-title fw-bold',
                htmlContainer: 'rtl-swal-content',
                confirmButton: 'btn btn-primary mx-2',
                cancelButton: 'btn btn-outline-secondary mx-2',
                actions: 'gap-2'
            },
            buttonsStyling: false,
            allowEscapeKey: true,
            allowOutsideClick: false,
            focusConfirm: false,
            focusCancel: true,
            showClass: {
                popup: 'animate__animated animate__bounceIn animate__faster'
            },
            hideClass: {
                popup: 'animate__animated animate__fadeOut animate__faster'
            },
            timer: 15000, // Auto-dismiss after 15 seconds
            timerProgressBar: true,
            didOpen: (popup) => {
                // Add sound effect for notifications (optional)
                this.playNotificationSound(notification.type);
                
                // Add hover effects for buttons
                const confirmBtn = popup.querySelector('.swal2-confirm');
                const cancelBtn = popup.querySelector('.swal2-cancel');
                
                if (confirmBtn) {
                    confirmBtn.addEventListener('mouseenter', () => {
                        confirmBtn.style.transform = 'scale(1.05)';
                    });
                    confirmBtn.addEventListener('mouseleave', () => {
                        confirmBtn.style.transform = 'scale(1)';
                    });
                }
                
                if (cancelBtn) {
                    cancelBtn.addEventListener('mouseenter', () => {
                        cancelBtn.style.transform = 'scale(1.05)';
                    });
                    cancelBtn.addEventListener('mouseleave', () => {
                        cancelBtn.style.transform = 'scale(1)';
                    });
                }
            }
        };

        // Add specific configurations based on notification type
        switch (notification.type) {
            case 'success':
                return {
                    ...baseConfig,
                    icon: 'success',
                    iconColor: '#28a745',
                    background: '#f8f9fa'
                };

            case 'error':
                return {
                    ...baseConfig,
                    icon: 'error',
                    iconColor: '#dc3545',
                    background: '#fff5f5',
                    timer: null // Don't auto-dismiss errors
                };

            case 'warning':
                return {
                    ...baseConfig,
                    icon: 'warning',
                    iconColor: '#ffc107',
                    background: '#fffbf0',
                    timer: 20000 // Longer time for warnings
                };

            case 'info':
            default:
                return {
                    ...baseConfig,
                    icon: 'info',
                    iconColor: '#17a2b8',
                    background: '#f0f9ff'
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
     * Format notification data for display
     * فرمت کردن داده‌های اعلان برای نمایش
     */
    formatNotificationData(data) {
        if (!data) return '';
        
        let html = '<div class="notification-details bg-light p-3 rounded mt-2">';
        
        // Order details
        if (data.orderId) {
            html += `<div class="detail-item mb-2">
                        <i class="fas fa-receipt text-primary me-2"></i>
                        <strong>شماره معامله:</strong> #${data.orderId}
                     </div>`;
        }
        
        if (data.customerName) {
            html += `<div class="detail-item mb-2">
                        <i class="fas fa-user text-success me-2"></i>
                        <strong>مشتری:</strong> ${data.customerName}
                     </div>`;
        }
        
        if (data.amount && data.currency) {
            html += `<div class="detail-item mb-2">
                        <i class="fas fa-money-bill-wave text-warning me-2"></i>
                        <strong>مبلغ:</strong> ${this.formatCurrency(data.amount)} ${data.currency}
                     </div>`;
        }
        
        // Document details
        if (data.documentId) {
            html += `<div class="detail-item mb-2">
                        <i class="fas fa-file-invoice text-info me-2"></i>
                        <strong>شماره سند:</strong> #${data.documentId}
                     </div>`;
        }
        
        // Account details
        if (data.accountNumber) {
            html += `<div class="detail-item mb-2">
                        <i class="fas fa-university text-secondary me-2"></i>
                        <strong>شماره حساب:</strong> ${data.accountNumber}
                     </div>`;
        }
        
        if (data.balance !== undefined) {
            html += `<div class="detail-item mb-2">
                        <i class="fas fa-balance-scale text-dark me-2"></i>
                        <strong>موجودی:</strong> ${this.formatCurrency(data.balance)}
                     </div>`;
        }
        
        html += '</div>';
        return html;
    }

    /**
     * Format currency for display
     * فرمت کردن ارز برای نمایش
     */
    formatCurrency(amount) {
        if (typeof amount !== 'number') return amount;
        return new Intl.NumberFormat('fa-IR').format(amount);
    }

    /**
     * Play notification sound
     * پخش صدای اعلان
     */
    playNotificationSound(type) {
        // Only play sound if user hasn't disabled it
        if (localStorage.getItem('notificationSounds') === 'false') return;
        
        try {
            // Create a subtle notification sound
            const audioContext = new (window.AudioContext || window.webkitAudioContext)();
            const oscillator = audioContext.createOscillator();
            const gainNode = audioContext.createGain();
            
            oscillator.connect(gainNode);
            gainNode.connect(audioContext.destination);
            
            // Different frequencies for different notification types
            const frequency = type === 'error' ? 300 : 
                             type === 'warning' ? 400 : 
                             type === 'success' ? 800 : 600;
            
            oscillator.frequency.setValueAtTime(frequency, audioContext.currentTime);
            oscillator.type = 'sine';
            
            gainNode.gain.setValueAtTime(0.1, audioContext.currentTime);
            gainNode.gain.exponentialRampToValueAtTime(0.01, audioContext.currentTime + 0.3);
            
            oscillator.start(audioContext.currentTime);
            oscillator.stop(audioContext.currentTime + 0.3);
        } catch (error) {
            // Silently fail if audio is not supported
            console.log('Notification sound not supported:', error);
        }
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
