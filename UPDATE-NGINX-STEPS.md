# راهنمای آپدیت فایل کانفیگ Nginx شما

## 📍 فایل شما:
`/etc/nginx/conf.d/taban-group.com.conf`

---

## 🚀 روش سریع (کپی و پیست)

### مرحله 1: بکاپ گرفتن
```bash
sudo cp /etc/nginx/conf.d/taban-group.com.conf /etc/nginx/conf.d/taban-group.com.conf.backup
```

### مرحله 2: ویرایش فایل
```bash
sudo nano /etc/nginx/conf.d/taban-group.com.conf
```

### مرحله 3: اضافه کردن خطوط

در بخش `location /` (بعد از خط `proxy_send_timeout 300s;`) این دو خط را اضافه کنید:

```nginx
        proxy_buffering off;
        proxy_request_buffering off;
```

در بخش `location /notificationHub` (بعد از خط `proxy_send_timeout 3600s;`) هم همین دو خط را اضافه کنید:

```nginx
        proxy_buffering off;
        proxy_request_buffering off;
```

### مرحله 4: ذخیره و خروج
- در nano: `Ctrl + X` سپس `Y` سپس `Enter`

### مرحله 5: تست و Reload
```bash
sudo nginx -t
sudo systemctl reload nginx
```

---

## 📝 تغییرات دقیق (خط به خط)

### بخش `location /` - قبل:
```nginx
    location / {
        proxy_pass http://127.0.0.1:5002;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 300s;
        proxy_connect_timeout 300s;
        proxy_send_timeout 300s;
    }
```

### بخش `location /` - بعد:
```nginx
    location / {
        proxy_pass http://127.0.0.1:5002;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 300s;
        proxy_connect_timeout 300s;
        proxy_send_timeout 300s;

        # ⭐ این دو خط را اضافه کنید:
        proxy_buffering off;
        proxy_request_buffering off;
    }
```

---

### بخش `location /notificationHub` - قبل:
```nginx
    location /notificationHub {
        proxy_pass http://127.0.0.1:5002;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }
```

### بخش `location /notificationHub` - بعد:
```nginx
    location /notificationHub {
        proxy_pass http://127.0.0.1:5002;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;

        # ⭐ این دو خط را اضافه کنید:
        proxy_buffering off;
        proxy_request_buffering off;
    }
```

---

## ✅ دستورات کامل (کپی و اجرا)

```bash
# 1. بکاپ
sudo cp /etc/nginx/conf.d/taban-group.com.conf /etc/nginx/conf.d/taban-group.com.conf.backup

# 2. ویرایش
sudo nano /etc/nginx/conf.d/taban-group.com.conf

# 3. بعد از ویرایش و ذخیره:
sudo nginx -t

# 4. اگر تست موفق بود:
sudo systemctl reload nginx

# 5. بررسی وضعیت
sudo systemctl status nginx
```

---

## 🎯 خلاصه تغییرات

**فقط 4 خط اضافه می‌شود:**
- 2 خط در `location /`
- 2 خط در `location /notificationHub`

**خطوط اضافه شده:**
```nginx
proxy_buffering off;
proxy_request_buffering off;
```

---

## ⚠️ نکات مهم

1. **بکاپ بگیرید** قبل از تغییر
2. **تست کنید** با `nginx -t` قبل از reload
3. **پورت شما 5002 است** (درست است)
4. **SSL تنظیمات شما حفظ می‌شود**

---

## 🔄 اگر مشکلی پیش آمد

```bash
# بازگردانی بکاپ
sudo cp /etc/nginx/conf.d/taban-group.com.conf.backup /etc/nginx/conf.d/taban-group.com.conf
sudo nginx -t
sudo systemctl reload nginx
```

---

**موفق باشید! 🚀**


