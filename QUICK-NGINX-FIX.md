# راهنمای سریع آپدیت Nginx (خلاصه)

## 🚀 روش سریع (3 دقیقه)

### 1️⃣ پیدا کردن فایل کانفیگ
```bash
# معمولاً یکی از این مسیرها:
/etc/nginx/conf.d/plesk.conf
/etc/nginx/plesk-http.conf
/etc/nginx/sites-available/default
```

### 2️⃣ بکاپ گرفتن
```bash
sudo cp /etc/nginx/conf.d/plesk.conf /etc/nginx/conf.d/plesk.conf.backup
```

### 3️⃣ ویرایش فایل
```bash
sudo nano /etc/nginx/conf.d/plesk.conf
```

### 4️⃣ اضافه کردن این دو خط در بخش `location /`:
```nginx
proxy_buffering off;
proxy_request_buffering off;
```

**مثال کامل:**
```nginx
location / {
    proxy_pass http://localhost:5000;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    # ... سایر تنظیمات ...
    
    # ⭐ این دو خط را اضافه کنید:
    proxy_buffering off;
    proxy_request_buffering off;
}
```

### 5️⃣ تست و Reload
```bash
# تست کانفیگ
sudo nginx -t

# اگر موفق بود، reload کنید
sudo systemctl reload nginx
```

---

## ✅ تمام! 

حالا overlay فوراً بسته می‌شود.

---

**برای راهنمای کامل و جزئیات بیشتر، فایل `NGINX-SETUP-GUIDE.md` را بخوانید.**

