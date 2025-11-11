@echo off
setlocal ENABLEDELAYEDEXPANSION
title 💾 Backup Database + Download File

:: === CONFIG ===
set SERVER=root@104.234.46.151
set BACKUP_DIR=/var/www/Taban_backUp
set DB_NAME=taban_db
set DB_USER=root
set DB_PASS=YOUR_DB_PASSWORD
set LOCAL_BACKUP_DIR=%~dp0db_backups

:: === Timestamp ===
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
set BACKUP_FILE=db_%DB_NAME%_%DATETIME%.sql.gz

echo =============================================
echo 🧠 Creating database backup on remote server
echo =============================================

ssh %SERVER% "mkdir -p %BACKUP_DIR%; mysqldump -u%DB_USER% -p%DB_PASS% %DB_NAME% | gzip > %BACKUP_DIR%/%BACKUP_FILE%"

if %ERRORLEVEL% NEQ 0 (
    echo ❌ Database backup failed.
    pause
    exit /b 1
)

echo =============================================
echo 📥 Downloading backup file
echo =============================================

if not exist "%LOCAL_BACKUP_DIR%" mkdir "%LOCAL_BACKUP_DIR%"
scp %SERVER%:%BACKUP_DIR%/%BACKUP_FILE% "%LOCAL_BACKUP_DIR%\"

if %ERRORLEVEL%==0 (
    echo ✅ Backup downloaded successfully:
    echo %LOCAL_BACKUP_DIR%\%BACKUP_FILE%
) else (
    echo ❌ Download failed. Check SSH connection or permissions.
)

echo =============================================
echo ✅ Done! Remote backup: %BACKUP_DIR%/%BACKUP_FILE%
echo =============================================

endlocal
pause
