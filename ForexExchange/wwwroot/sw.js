// Service Worker for Push Notifications
// Web Worker برای اعلان‌های فشاری

const CACHE_NAME = 'taban-forex-v1';
const NOTIFICATION_TAG = 'taban-notification';

// Install service worker
self.addEventListener('install', event => {
    console.log('Service Worker: Install');
    event.waitUntil(
        caches.open(CACHE_NAME).then(cache => {
            console.log('Service Worker: Caching files');
            return cache.addAll([
                '/',
                '/css/site.css',
                '/css/modern-notifications.css',
                '/js/admin-notifications.js',
                '/favicon/favicon-32x32.png',
                '/favicon/apple-touch-icon.png'
            ]);
        })
    );
});

// Activate service worker
self.addEventListener('activate', event => {
    console.log('Service Worker: Activate');
    event.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames.map(cache => {
                    if (cache !== CACHE_NAME) {
                        console.log('Service Worker: Clearing old cache');
                        return caches.delete(cache);
                    }
                })
            );
        })
    );
});

// Handle push events
self.addEventListener('push', event => {
    console.log('Service Worker: Push received');
    
    let data = {
        title: 'سامانه معاملات تابان',
        body: 'اعلان جدید دریافت شد',
        icon: '/favicon/apple-touch-icon.png',
        badge: '/favicon/favicon-32x32.png',
        tag: NOTIFICATION_TAG,
        data: {}
    };

    if (event.data) {
        try {
            const pushData = event.data.json();
            data = {
                ...data,
                title: pushData.title || data.title,
                body: pushData.message || pushData.body || data.body,
                icon: pushData.icon || data.icon,
                tag: pushData.tag || data.tag,
                data: pushData.data || {},
                badge: data.badge,
                requireInteraction: pushData.type === 'error' || pushData.type === 'warning',
                silent: false,
                timestamp: Date.now(),
                actions: [
                    {
                        action: 'view',
                        title: 'مشاهده',
                        icon: '/favicon/favicon-32x32.png'
                    },
                    {
                        action: 'dismiss',
                        title: 'بستن'
                    }
                ]
            };

            // Add emoji based on notification type
            if (pushData.type) {
                switch (pushData.type) {
                    case 'success':
                        data.body = '✅ ' + data.body;
                        break;
                    case 'warning':
                        data.body = '⚠️ ' + data.body;
                        break;
                    case 'error':
                        data.body = '❌ ' + data.body;
                        break;
                    case 'info':
                        data.body = 'ℹ️ ' + data.body;
                        break;
                }
            }
        } catch (e) {
            console.error('Service Worker: Error parsing push data:', e);
        }
    }

    event.waitUntil(
        self.registration.showNotification(data.title, {
            body: data.body,
            icon: data.icon,
            badge: data.badge,
            tag: data.tag,
            data: data.data,
            requireInteraction: data.requireInteraction,
            silent: data.silent,
            timestamp: data.timestamp,
            actions: data.actions,
            dir: 'rtl',
            lang: 'fa'
        })
    );
});

// Handle notification click
self.addEventListener('notificationclick', event => {
    console.log('Service Worker: Notification click received');
    
    event.notification.close();

    if (event.action === 'dismiss') {
        return;
    }

    // Handle notification click
    event.waitUntil(
        clients.matchAll({
            type: 'window',
            includeUncontrolled: true
        }).then(clientList => {
            const data = event.notification.data;
            
            // Determine URL to open
            let url = '/';
            if (data) {
                if (data.orderId) {
                    url = `/Orders/Details/${data.orderId}`;
                } else if (data.customerId) {
                    url = `/Customers/Details/${data.customerId}`;
                } else if (data.documentId) {
                    url = `/AccountingDocuments/Details/${data.documentId}`;
                } else if (data.bankAccountId) {
                    url = `/BankAccount/Details/${data.bankAccountId}`;
                }
            }

            // Check if app is already open
            for (const client of clientList) {
                if (client.url.includes(self.location.origin) && 'focus' in client) {
                    client.focus();
                    client.postMessage({
                        type: 'NOTIFICATION_CLICK',
                        url: url,
                        data: data
                    });
                    return;
                }
            }

            // Open new window
            if (clients.openWindow) {
                return clients.openWindow(self.location.origin + url);
            }
        })
    );
});

// Handle notification close
self.addEventListener('notificationclose', event => {
    console.log('Service Worker: Notification closed');
    
    // Optional: Track notification dismissal
    event.waitUntil(
        clients.matchAll({
            type: 'window',
            includeUncontrolled: true
        }).then(clientList => {
            clientList.forEach(client => {
                client.postMessage({
                    type: 'NOTIFICATION_CLOSED',
                    data: event.notification.data
                });
            });
        })
    );
});

// Handle background sync (for offline support)
self.addEventListener('sync', event => {
    console.log('Service Worker: Background sync');
    
    if (event.tag === 'background-sync') {
        event.waitUntil(
            // Perform background tasks here
            Promise.resolve()
        );
    }
});

// Handle messages from main thread
self.addEventListener('message', event => {
    console.log('Service Worker: Message received', event.data);
    
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});
