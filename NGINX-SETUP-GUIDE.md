# راهنمای قدم به قدم آپدیت فایل کانفیگ Nginx

## 📋 پیش‌نیازها
- دسترسی root یا sudo به سرور Linux
- آشنایی با ویرایش فایل‌های متنی در Linux (nano, vi, vim)
- دانستن مسیر فایل کانفیگ nginx شما

---

## 🔍 مرحله 1: پیدا کردن فایل کانفیگ Nginx

### گزینه 1: اگر از Plesk استفاده می‌کنید
```bash
# فایل کانفیگ معمولاً در این مسیر است:
/etc/nginx/conf.d/plesk.conf
# یا
/etc/nginx/plesk-http.conf
```

### گزینه 2: اگر کانفیگ دستی دارید
```bash
# فایل کانفیگ اصلی
/etc/nginx/nginx.conf

# یا فایل‌های site-specific
/etc/nginx/sites-available/your-site.conf
/etc/nginx/sites-enabled/your-site.conf
```

### پیدا کردن فایل کانفیگ:
```bash
# جستجو برای فایل‌های کانفیگ
sudo find /etc/nginx -name "*.conf" -type f

# یا بررسی کدام فایل در حال استفاده است
sudo nginx -T | grep "server_name your-domain.com"
```

---

## 📝 مرحله 2: بکاپ گرفتن از فایل کانفیگ

**⚠️ مهم: همیشه قبل از تغییر، بکاپ بگیرید!**

```bash
# بکاپ از فایل کانفیگ
sudo cp /etc/nginx/conf.d/plesk.conf /etc/nginx/conf.d/plesk.conf.backup

# یا اگر فایل دیگری دارید:
sudo cp /path/to/your/config.conf /path/to/your/config.conf.backup
```

---

## ✏️ مرحله 3: ویرایش فایل کانفیگ

### باز کردن فایل با nano (راحت‌تر برای مبتدیان):
```bash
sudo nano /etc/nginx/conf.d/plesk.conf
```

### یا با vi/vim:
```bash
sudo vi /etc/nginx/conf.d/plesk.conf
```

---

## 🔧 مرحله 4: اضافه کردن تنظیمات

### پیدا کردن بخش `location /`:

در فایل کانفیگ، بخشی شبیه این را پیدا کنید:

```nginx
location / {
    proxy_pass http://localhost:5000;
    # ... سایر تنظیمات
}
```

### اضافه کردن تنظیمات جدید:

**قبل از تغییر:**
```nginx
location / {
    proxy_pass http://localhost:5000;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    # ...
}
```

**بعد از تغییر (اضافه کردن خطوط جدید):**
```nginx
location / {
    proxy_pass http://localhost:5000;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection keep-alive;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_cache_bypass $http_upgrade;

    # ⭐ مهم: غیرفعال کردن buffering برای پاسخ فوری
    proxy_buffering off;
    proxy_request_buffering off;
}
```

---

## 🔌 مرحله 5: اضافه کردن تنظیمات SignalR (WebSocket)

### پیدا کردن یا اضافه کردن بخش `location /notificationHub`:

اگر این بخش وجود ندارد، آن را اضافه کنید:

```nginx
# SignalR WebSocket support
location /notificationHub {
    proxy_pass http://localhost:5000;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_cache_bypass $http_upgrade;
    
    # غیرفعال کردن buffering برای WebSocket
    proxy_buffering off;
    proxy_request_buffering off;
    
    # تنظیمات timeout برای WebSocket
    proxy_read_timeout 86400;
    proxy_send_timeout 86400;
}
```

---

## ✅ مرحله 6: تست کردن کانفیگ

قبل از reload کردن nginx، حتماً کانفیگ را تست کنید:

```bash
# تست syntax کانفیگ
sudo nginx -t
```

**خروجی موفق:**
```
nginx: the configuration file /etc/nginx/nginx.conf syntax is ok
nginx: configuration file /etc/nginx/nginx.conf test is successful
```

**اگر خطا داشت:**
- خطا را بخوانید و فایل را دوباره ویرایش کنید
- معمولاً مشکل از syntax (مثلاً نقطه‌ویرگول یا آکولاد فراموش شده)

---

## 🔄 مرحله 7: Reload کردن Nginx

بعد از تست موفق، nginx را reload کنید:

```bash
# Reload کردن nginx (بدون قطع شدن سرویس)
sudo systemctl reload nginx

# یا
sudo service nginx reload

# یا
sudo nginx -s reload
```

**⚠️ اگر reload کار نکرد:**
```bash
# Restart کامل (سرویس را قطع و دوباره شروع می‌کند)
sudo systemctl restart nginx
```

---

## 🧪 مرحله 8: تست کردن عملکرد

### 1. تست دستی:
- به سایت بروید
- یک معامله ایجاد کنید
- یک معامله حذف کنید
- بررسی کنید که overlay فوراً بسته می‌شود

### 2. بررسی لاگ‌ها:
```bash
# مشاهده لاگ‌های nginx
sudo tail -f /var/log/nginx/error.log
sudo tail -f /var/log/nginx/access.log
```

---

## 📋 مثال کامل فایل کانفیگ

```nginx
server {
    listen 80;
    server_name taban-group.com www.taban-group.com;

    # Proxy settings for ASP.NET Core
    location / {
        proxy_pass http://localhost:5000;  # پورت برنامه شما
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;

        # ⭐ مهم: غیرفعال کردن buffering
        proxy_buffering off;
        proxy_request_buffering off;
    }

    # SignalR WebSocket support
    location /notificationHub {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        
        proxy_buffering off;
        proxy_request_buffering off;
        
        proxy_read_timeout 86400;
        proxy_send_timeout 86400;
    }
}
```

---

## 🆘 عیب‌یابی

### مشکل 1: nginx -t خطا می‌دهد
```bash
# بررسی خطا
sudo nginx -t

# معمولاً خطا از این موارد است:
# - نقطه‌ویرگول (;) فراموش شده
# - آکولاد باز/بسته ({}) درست نیست
# - مسیر فایل اشتباه است
```

### مشکل 2: بعد از reload سایت کار نمی‌کند
```bash
# بررسی وضعیت nginx
sudo systemctl status nginx

# بررسی لاگ‌های خطا
sudo tail -50 /var/log/nginx/error.log

# بازگردانی بکاپ
sudo cp /etc/nginx/conf.d/plesk.conf.backup /etc/nginx/conf.d/plesk.conf
sudo nginx -t
sudo systemctl reload nginx
```

### مشکل 3: پورت 5000 درست نیست
```bash
# بررسی پورت برنامه شما
sudo netstat -tlnp | grep :5000
# یا
sudo ss -tlnp | grep :5000

# اگر پورت دیگری است (مثلاً 5001)، در کانفیگ تغییر دهید:
# proxy_pass http://localhost:5001;
```

---

## 📞 نکات مهم

1. **همیشه بکاپ بگیرید** قبل از تغییر
2. **همیشه `nginx -t` بزنید** قبل از reload
3. **پورت برنامه را بررسی کنید** (ممکن است 5000 نباشد)
4. **اگر از SSL استفاده می‌کنید** (HTTPS)، تنظیمات را در بخش `server` با `listen 443` هم اضافه کنید

---

## ✅ چک‌لیست نهایی

- [ ] بکاپ از فایل کانفیگ گرفته شد
- [ ] فایل کانفیگ ویرایش شد
- [ ] `proxy_buffering off` اضافه شد
- [ ] `proxy_request_buffering off` اضافه شد
- [ ] تنظیمات SignalR اضافه شد
- [ ] `nginx -t` بدون خطا اجرا شد
- [ ] nginx reload شد
- [ ] سایت کار می‌کند
- [ ] Overlay فوراً بسته می‌شود

---

**موفق باشید! 🚀**

