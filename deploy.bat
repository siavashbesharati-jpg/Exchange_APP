@echo off
setlocal ENABLEDELAYEDEXPANSION
title 🚀 Micro Deploy Taban (.NET 9.0)

:: === CONFIG ===
set SERVER=root@104.234.46.151
set APP_DIR=/var/www/taban
set BACKUP_DIR=/var/www/Taban_backUp
set SERVICE=taban.service
set PROJECT_PATH=%~dp0
set FRAMEWORK=net9.0
set LOCAL_PUBLISH_DIR=%PROJECT_PATH%bin\Release\%FRAMEWORK%

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

echo =============================================
echo 🧱 Publishing project (Release, net9.0)
echo =============================================

dotnet publish -c Release
if errorlevel 1 (
    echo ❌ Build failed.
    exit /b 1
)

echo =============================================
echo 💾 Backing up current binaries on server
echo =============================================
ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% ^
  "mkdir -p %BACKUP_DIR%/bin_%DATETIME% && \
   cp %APP_DIR%/ForexExchange.* %BACKUP_DIR%/bin_%DATETIME%/ 2>/dev/null || echo (some files missing, skipping)"

echo =============================================
echo 🚦 Stopping service
echo =============================================
ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "systemctl stop %SERVICE%"

echo =============================================
echo 🚚 Uploading updated files
echo =============================================
for %%F in (ForexExchange.dll ForexExchange.exe ForexExchange.pdb) do (
    echo 🔄 Uploading %%F ...
    scp -o ConnectTimeout=10 -o BatchMode=yes "%LOCAL_PUBLISH_DIR%\%%F" %SERVER%:%APP_DIR%/
)

echo =============================================
echo 🔁 Restarting service
echo =============================================
ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "systemctl start %SERVICE% && echo ✅ Service restarted"

echo =============================================
echo ✅ Done! Backup saved as: bin_%DATETIME%
echo =============================================

endlocal
pause
