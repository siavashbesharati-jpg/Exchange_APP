@echo off
setlocal ENABLEDELAYEDEXPANSION
title ⚡ Restart Service + Download Logs

:: === CONFIG ===
set SERVER=root@104.234.46.151
set APP_DIR=/var/www/taban
set LOGS_DIR=%APP_DIR%/Logs
set SERVICE=taban.service

:: === LOCAL DESTINATION ===
set LOCAL_LOGS_DIR=%~dp0server_logs

echo =============================================
echo 🔄 Restarting service: %SERVICE% on %SERVER%
echo =============================================

:: Restart service remotely
ssh %SERVER% "sudo systemctl restart %SERVICE% && sudo systemctl status %SERVICE% --no-pager -l | head -n 10"

if %ERRORLEVEL%==0 (
    echo ✅ Service restarted successfully.
) else (
    echo ❌ Failed to restart service. Check systemctl logs.
    pause
    exit /b 1
)

echo.
echo =============================================
echo 🛰  Downloading logs from %SERVER%:%LOGS_DIR%
echo =============================================

:: Create local logs folder if not exists
if not exist "%LOCAL_LOGS_DIR%" mkdir "%LOCAL_LOGS_DIR%"

:: Copy logs
scp -r %SERVER%:%LOGS_DIR%/* "%LOCAL_LOGS_DIR%"

if %ERRORLEVEL%==0 (
    echo ✅ Logs successfully downloaded to:
    echo %LOCAL_LOGS_DIR%
) else (
    echo ❌ Error downloading logs. Check SSH connection or permissions.
)

pause
