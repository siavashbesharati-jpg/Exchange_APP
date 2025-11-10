#!/bin/bash

# اسکریپت کمکی برای آپدیت کانفیگ Nginx
# استفاده: sudo bash update-nginx-config.sh

echo "=========================================="
echo "راهنمای آپدیت کانفیگ Nginx"
echo "=========================================="
echo ""

# رنگ‌ها برای خروجی
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# پیدا کردن فایل کانفیگ
echo -e "${YELLOW}مرحله 1: پیدا کردن فایل کانفیگ...${NC}"
echo ""

# بررسی فایل‌های معمول
CONFIG_FILES=(
    "/etc/nginx/conf.d/plesk.conf"
    "/etc/nginx/plesk-http.conf"
    "/etc/nginx/sites-available/default"
    "/etc/nginx/nginx.conf"
)

CONFIG_FILE=""
for file in "${CONFIG_FILES[@]}"; do
    if [ -f "$file" ]; then
        echo -e "${GREEN}✓ پیدا شد: $file${NC}"
        read -p "آیا این فایل را می‌خواهید ویرایش کنید؟ (y/n): " confirm
        if [ "$confirm" = "y" ] || [ "$confirm" = "Y" ]; then
            CONFIG_FILE="$file"
            break
        fi
    fi
done

if [ -z "$CONFIG_FILE" ]; then
    echo -e "${RED}✗ فایل کانفیگ پیدا نشد.${NC}"
    echo "لطفاً مسیر فایل کانفیگ خود را وارد کنید:"
    read -p "مسیر فایل: " CONFIG_FILE
fi

if [ ! -f "$CONFIG_FILE" ]; then
    echo -e "${RED}✗ فایل پیدا نشد: $CONFIG_FILE${NC}"
    exit 1
fi

echo ""
echo -e "${GREEN}✓ فایل انتخاب شده: $CONFIG_FILE${NC}"
echo ""

# بکاپ گرفتن
echo -e "${YELLOW}مرحله 2: گرفتن بکاپ...${NC}"
BACKUP_FILE="${CONFIG_FILE}.backup.$(date +%Y%m%d_%H%M%S)"
sudo cp "$CONFIG_FILE" "$BACKUP_FILE"
echo -e "${GREEN}✓ بکاپ گرفته شد: $BACKUP_FILE${NC}"
echo ""

# بررسی وجود تنظیمات
echo -e "${YELLOW}مرحله 3: بررسی تنظیمات موجود...${NC}"

if grep -q "proxy_buffering off" "$CONFIG_FILE"; then
    echo -e "${GREEN}✓ تنظیمات buffering از قبل وجود دارد${NC}"
    read -p "آیا می‌خواهید دوباره اضافه کنید؟ (y/n): " redo
    if [ "$redo" != "y" ] && [ "$redo" != "Y" ]; then
        echo "تغییری اعمال نشد."
        exit 0
    fi
fi

echo ""
echo -e "${YELLOW}مرحله 4: نمایش محتوای فعلی location / ...${NC}"
echo "----------------------------------------"
sudo grep -A 10 "location /" "$CONFIG_FILE" | head -15
echo "----------------------------------------"
echo ""

# نمایش دستورالعمل
echo -e "${YELLOW}دستورالعمل:${NC}"
echo "1. فایل را با دستور زیر باز کنید:"
echo -e "   ${GREEN}sudo nano $CONFIG_FILE${NC}"
echo ""
echo "2. در بخش 'location /' این دو خط را اضافه کنید:"
echo -e "   ${GREEN}proxy_buffering off;${NC}"
echo -e "   ${GREEN}proxy_request_buffering off;${NC}"
echo ""
echo "3. اگر بخش 'location /notificationHub' ندارید، آن را اضافه کنید"
echo ""
echo "4. بعد از ذخیره، این دستورات را اجرا کنید:"
echo -e "   ${GREEN}sudo nginx -t${NC}"
echo -e "   ${GREEN}sudo systemctl reload nginx${NC}"
echo ""

read -p "آیا می‌خواهید فایل را الان باز کنیم؟ (y/n): " open_now
if [ "$open_now" = "y" ] || [ "$open_now" = "Y" ]; then
    sudo nano "$CONFIG_FILE"
fi

echo ""
echo -e "${YELLOW}مرحله 5: تست کردن کانفیگ...${NC}"
read -p "آیا می‌خواهید الان تست کنیم؟ (y/n): " test_now
if [ "$test_now" = "y" ] || [ "$test_now" = "Y" ]; then
    if sudo nginx -t; then
        echo ""
        echo -e "${GREEN}✓ کانفیگ معتبر است!${NC}"
        read -p "آیا می‌خواهید nginx را reload کنید؟ (y/n): " reload_now
        if [ "$reload_now" = "y" ] || [ "$reload_now" = "Y" ]; then
            sudo systemctl reload nginx
            echo -e "${GREEN}✓ Nginx reload شد!${NC}"
        fi
    else
        echo ""
        echo -e "${RED}✗ خطا در کانفیگ!${NC}"
        echo "لطفاً فایل را بررسی کنید یا بکاپ را بازگردانید:"
        echo "sudo cp $BACKUP_FILE $CONFIG_FILE"
    fi
fi

echo ""
echo -e "${GREEN}=========================================="
echo "تمام! راهنمای کامل در فایل NGINX-SETUP-GUIDE.md موجود است"
echo "==========================================${NC}"

