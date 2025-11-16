#!/bin/bash
BACKUP_DIR="/var/www/Taban_backUp"
DB_NAME="taban_db"
DB_USER="root"
DB_PASS="YOUR_DB_PASSWORD"
MAX_BACKUPS=48

TIMESTAMP=$(date +'%Y-%m-%d_%H-%M')
BACKUP_FILE="$BACKUP_DIR/db_${DB_NAME}_$TIMESTAMP.sql.gz"

# آخرین فایل بکاپ
LAST_BACKUP=$(ls -t $BACKUP_DIR/db_${DB_NAME}_*.sql.gz 2>/dev/null | head -n 1)

# گرفتن بکاپ جدید
mysqldump -u$DB_USER -p$DB_PASS $DB_NAME | gzip > "$BACKUP_FILE"
if [ $? -ne 0 ]; then
    echo "❌ Database backup failed!"
    exit 1
fi

# مقایسه با آخرین بکاپ
if [ -f "$LAST_BACKUP" ]; then
    LAST_SIZE=$(stat -c%s "$LAST_BACKUP")
    NEW_SIZE=$(stat -c%s "$BACKUP_FILE")
    if [ "$LAST_SIZE" -eq "$NEW_SIZE" ]; then
        echo "ℹ️ Database not changed. Removing new backup."
        rm -f "$BACKUP_FILE"
        exit 0
    fi
fi

echo "✅ Backup saved: $BACKUP_FILE"

# نگهداری فقط MAX_BACKUPS فایل اخیر
ls -t $BACKUP_DIR/db_${DB_NAME}_*.sql.gz | tail -n +$MAX_BACKUPS | xargs -r rm -f
