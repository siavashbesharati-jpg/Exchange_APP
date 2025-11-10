@echo off
setlocal ENABLEDELAYEDEXPANSION

:: --- Generate timestamp ---
for /f "tokens=1-4 delims=/ " %%a in ("%date%") do (
    set YYYY=%%d
    set MM=%%b
    set DD=%%c
)
for /f "tokens=1-4 delims=:." %%a in ("%time%") do (
    set HH=%%a
    set MN=%%b
    set SS=%%c
)
set HH=%HH: =0%
set DATETIME=%YYYY%-%MM%-%DD%_%HH%-%MN%-%SS%

echo Starting backup: %DATETIME%

:: --- Run your same commands ---
ssh -o ConnectTimeout=10 -o BatchMode=yes root@104.234.46.151 "systemctl stop taban.service && echo ✅ Service stopped"
ssh -o ConnectTimeout=10 -o BatchMode=yes root@104.234.46.151 "cp /var/www/taban/ForexExchange.db /var/www/Taban_backUp/ForexExchange.db.%DATETIME%"
ssh -o ConnectTimeout=10 -o BatchMode=yes root@104.234.46.151 "for f in /var/www/taban/ForexExchange.db-wal /var/www/taban/ForexExchange.db-shm; do [ -f $f ] && cp $f /var/www/Taban_backUp/$(basename $f).%DATETIME%; done"
ssh -o ConnectTimeout=10 -o BatchMode=yes root@104.234.46.151 "systemctl start taban.service && echo ✅ Service restarted"
ssh -o ConnectTimeout=10 -o BatchMode=yes root@104.234.46.151 "ls -lh /var/www/Taban_backUp/"

echo ✅ Done! Backups saved with timestamp: %DATETIME%
timeout /t 3 >nul
