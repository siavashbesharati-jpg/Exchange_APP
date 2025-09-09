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
        this.soundEnabled = true; // Enable sound by default
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

        // Add haptic feedback for mobile devices
        if (navigator.vibrate) {
            navigator.vibrate(200);
        }

        Swal.fire(config).then((result) => {
            if (result.isConfirmed) {
                // Smooth page refresh with loading animation
                this.showLoadingOverlay();
                setTimeout(() => {
                    this.refreshCurrentPage();
                }, 500);
            }
        });
    }

    /**
     * Get notification configuration based on type
     * دریافت تنظیمات اعلان بر اساس نوع
     */
    getNotificationConfig(notification) {
        const baseConfig = {
            title: this.createModernTitle(notification),
            html: this.createModernContent(notification),
            icon: false, // We'll use custom icons
            width: '480px',
            padding: '0',
            showConfirmButton: true,
            showCancelButton: true,
            confirmButtonText: '<i class="fas fa-sync-alt me-2"></i>بروزرسانی صفحه',
            cancelButtonText: '<i class="fas fa-check me-2"></i>متوجه شدم',
            customClass: {
                popup: `modern-notification-popup ${this.getThemeClass(notification.type)} shadow-2xl`,
                title: 'modern-notification-title',
                htmlContainer: 'modern-notification-content',
                confirmButton: 'modern-btn modern-btn-primary',
                cancelButton: 'modern-btn modern-btn-secondary',
                actions: 'modern-notification-actions'
            },
            buttonsStyling: false,
            allowEscapeKey: true,
            allowOutsideClick: true,
            focusConfirm: false,
            focusCancel: true,
            showClass: {
                popup: 'animate__animated animate__zoomIn animate__faster'
            },
            hideClass: {
                popup: 'animate__animated animate__zoomOut animate__faster'
            },
            timer: this.getTimerForType(notification.type),
            timerProgressBar: true,
            backdrop: 'rgba(0, 0, 0, 0.4)',
            didOpen: (popup) => {
                this.initializeModernNotification(popup, notification);
            },
            didClose: () => {
                this.cleanupNotificationEffects();
            }
        };

        return baseConfig;
    }

    /**
     * Create modern title with icons and styling
     */
    createModernTitle(notification) {
        const iconConfig = this.getIconConfig(notification.type);
        return `
            <div class="modern-title-container">
                <div class="modern-icon-wrapper ${iconConfig.class}">
                    <i class="${iconConfig.icon}"></i>
                </div>
                <span class="modern-title-text">${notification.title || 'اعلان'}</span>
            </div>
        `;
    }

    /**
     * Create modern notification content
     */
    createModernContent(notification) {
        const timestamp = new Date(notification.timestamp).toLocaleString('fa-IR', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        });

        return `
            <div class="modern-notification-body">
                <div class="notification-message-modern">
                    ${this.formatMessage(notification.message || '')}
                </div>
                
                ${notification.data ? this.createDataSection(notification.data) : ''}
                
                <div class="notification-footer-modern">
                    <div class="timestamp-container">
                        <i class="fas fa-clock timestamp-icon"></i>
                        <span class="timestamp-text">${timestamp}</span>
                    </div>
                    <div class="notification-badge ${this.getBadgeClass(notification.type)}">
                        ${this.getTypeLabel(notification.type)}
                    </div>
                </div>
            </div>
        `;
    }

    /**
     * Format message with modern styling
     */
    formatMessage(message) {
        // Replace line breaks with styled breaks
        return message.replace(/\n\n/g, '<div class="message-break"></div>')
                     .replace(/\n/g, '<br>');
    }

    /**
     * Create data section with modern cards
     */
    createDataSection(data) {
        if (!data) return '';

        const dataItems = Object.entries(data)
            .filter(([key, value]) => key !== 'timestamp' && value !== null && value !== undefined)
            .map(([key, value]) => {
                const label = this.getDataLabel(key);
                const formattedValue = this.formatDataValue(key, value);
                
                return `
                    <div class="data-item">
                        <span class="data-label">${label}:</span>
                        <span class="data-value">${formattedValue}</span>
                    </div>
                `;
            })
            .join('');

        return dataItems ? `
            <div class="notification-data-section">
                <div class="data-grid">
                    ${dataItems}
                </div>
            </div>
        ` : '';
    }

    /**
     * Initialize modern notification with animations and effects
     */
    initializeModernNotification(popup, notification) {
        // Play modern notification sound
        this.playModernNotificationSound(notification.type);
        
        // Add particle effect for success notifications
        if (notification.type === 'success') {
            this.addParticleEffect(popup);
        }
        
        // Add pulse effect for errors
        if (notification.type === 'error') {
            this.addPulseEffect(popup);
        }
        
        // Add interactive hover effects
        this.addInteractiveEffects(popup);
        
        // Auto-scroll to important content if notification is tall
        this.autoScrollContent(popup);
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

    // Modern Notification Helper Methods
    
    /**
     * Get icon configuration for notification type
     */
    getIconConfig(type) {
        const configs = {
            'success': { icon: 'fas fa-check-circle', class: 'icon-success' },
            'error': { icon: 'fas fa-exclamation-triangle', class: 'icon-error' },
            'warning': { icon: 'fas fa-exclamation-circle', class: 'icon-warning' },
            'info': { icon: 'fas fa-info-circle', class: 'icon-info' }
        };
        return configs[type] || configs['info'];
    }

    /**
     * Get theme class for notification type
     */
    getThemeClass(type) {
        const themes = {
            'success': 'theme-success',
            'error': 'theme-error', 
            'warning': 'theme-warning',
            'info': 'theme-info'
        };
        return themes[type] || themes['info'];
    }

    /**
     * Get timer duration for notification type
     */
    getTimerForType(type) {
        const timers = {
            'success': 8000,
            'error': null, // No auto-dismiss for errors
            'warning': 12000,
            'info': 10000
        };
        return timers[type] !== undefined ? timers[type] : 10000;
    }

    /**
     * Get badge class for notification type
     */
    getBadgeClass(type) {
        const badges = {
            'success': 'badge-success',
            'error': 'badge-error',
            'warning': 'badge-warning', 
            'info': 'badge-info'
        };
        return badges[type] || badges['info'];
    }

    /**
     * Get type label in Persian
     */
    getTypeLabel(type) {
        const labels = {
            'success': 'موفق',
            'error': 'خطا',
            'warning': 'هشدار',
            'info': 'اطلاع'
        };
        return labels[type] || labels['info'];
    }

    /**
     * Get data label in Persian
     */
    getDataLabel(key) {
        const labels = {
            'orderId': 'شماره سفارش',
            'customerId': 'شناسه مشتری',
            'customerName': 'نام مشتری',
            'amount': 'مبلغ',
            'fromCurrency': 'ارز مبدا',
            'toCurrency': 'ارز مقصد',
            'documentId': 'شناسه سند',
            'documentType': 'نوع سند',
            'bankAccountId': 'شناسه حساب',
            'bankName': 'نام بانک',
            'accountNumber': 'شماره حساب',
            'currencyCode': 'کد ارز',
            'action': 'عملیات'
        };
        return labels[key] || key;
    }

    /**
     * Format data value based on key type
     */
    formatDataValue(key, value) {
        if (key === 'amount' && typeof value === 'number') {
            return value.toLocaleString('fa-IR');
        }
        if (key.includes('Id') && typeof value === 'number') {
            return `#${value}`;
        }
        return value;
    }

    /**
     * Play modern notification sound
     */
    playModernNotificationSound(type) {
        if (!this.soundEnabled) return;

        // Create audio context for modern sound synthesis
        try {
            const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
            const oscillator = audioCtx.createOscillator();
            const gainNode = audioCtx.createGain();

            oscillator.connect(gainNode);
            gainNode.connect(audioCtx.destination);

            // Different frequencies for different notification types
            const frequencies = {
                'success': [523.25, 659.25, 783.99], // C5, E5, G5 (happy chord)
                'error': [220, 185], // A3, F#3 (dissonant)
                'warning': [440, 554.37], // A4, C#5 (attention)
                'info': [523.25, 698.46] // C5, F5 (neutral)
            };

            const freqs = frequencies[type] || frequencies['info'];
            
            oscillator.frequency.setValueAtTime(freqs[0], audioCtx.currentTime);
            if (freqs[1]) {
                oscillator.frequency.setValueAtTime(freqs[1], audioCtx.currentTime + 0.1);
            }
            if (freqs[2]) {
                oscillator.frequency.setValueAtTime(freqs[2], audioCtx.currentTime + 0.2);
            }

            gainNode.gain.setValueAtTime(0.1, audioCtx.currentTime);
            gainNode.gain.exponentialRampToValueAtTime(0.01, audioCtx.currentTime + 0.3);

            oscillator.start(audioCtx.currentTime);
            oscillator.stop(audioCtx.currentTime + 0.3);
        } catch (e) {
            console.log('Audio not supported');
        }
    }

    /**
     * Add particle effect for success notifications
     */
    addParticleEffect(popup) {
        const particles = document.createElement('div');
        particles.className = 'particle-container';
        particles.innerHTML = Array.from({length: 20}, () => 
            '<div class="particle"></div>'
        ).join('');
        
        popup.appendChild(particles);
        
        setTimeout(() => {
            if (particles.parentNode) {
                particles.parentNode.removeChild(particles);
            }
        }, 3000);
    }

    /**
     * Add pulse effect for error notifications
     */
    addPulseEffect(popup) {
        popup.classList.add('notification-pulse');
        setTimeout(() => {
            popup.classList.remove('notification-pulse');
        }, 2000);
    }

    /**
     * Add interactive hover effects
     */
    addInteractiveEffects(popup) {
        const buttons = popup.querySelectorAll('.modern-btn');
        buttons.forEach(btn => {
            btn.addEventListener('mouseenter', () => {
                btn.style.transform = 'translateY(-2px) scale(1.02)';
                btn.style.boxShadow = '0 8px 25px rgba(0,0,0,0.15)';
            });
            
            btn.addEventListener('mouseleave', () => {
                btn.style.transform = 'translateY(0) scale(1)';
                btn.style.boxShadow = '';
            });
        });
    }

    /**
     * Auto-scroll content if notification is tall
     */
    autoScrollContent(popup) {
        const content = popup.querySelector('.modern-notification-content');
        if (content && content.scrollHeight > content.clientHeight) {
            content.scrollTop = 0;
        }
    }

    /**
     * Show loading overlay for smooth transitions
     */
    showLoadingOverlay() {
        const overlay = document.createElement('div');
        overlay.className = 'loading-overlay';
        overlay.innerHTML = `
            <div class="loading-spinner">
                <div class="spinner-ring"></div>
                <div class="loading-text">در حال بروزرسانی...</div>
            </div>
        `;
        document.body.appendChild(overlay);
    }

    /**
     * Cleanup notification effects
     */
    cleanupNotificationEffects() {
        // Remove any particle effects
        const particles = document.querySelectorAll('.particle-container');
        particles.forEach(p => p.remove());
        
        // Remove loading overlays
        const overlays = document.querySelectorAll('.loading-overlay');
        overlays.forEach(o => o.remove());
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
