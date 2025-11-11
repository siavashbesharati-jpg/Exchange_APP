@echo off
setlocal ENABLEDELAYEDEXPANSION
title 🛑 Stop Remote Service

:: === CONFIG ===
set SERVER=root@104.234.46.151
set SERVICE=taban.service

echo =============================================
echo 🔻 Stopping service: %SERVICE% on %SERVER%
echo =============================================

ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "systemctl stop %SERVICE% && systemctl status %SERVICE% --no-pager -l | head -n 10"

if %ERRORLEVEL%==0 (
    echo ✅ Service stopped successfully!
) else (
    echo ❌ Failed to stop service. Check SSH or permissions.
)

pause
endlocal
