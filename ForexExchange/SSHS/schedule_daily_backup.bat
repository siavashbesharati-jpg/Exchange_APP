@echo off
setlocal ENABLEDELAYEDEXPANSION
title ⏰ Schedule Daily Backup - ForexExchange

REM === This script sets up Windows Task Scheduler for daily automated backups ===

echo.
echo =====================================================
echo ⏰ Automated Daily Backup Scheduler
echo =====================================================
echo.
echo This script will set up Windows Task Scheduler to backup
echo your ForexExchange database automatically every day.
echo.

REM === Check for admin privileges ===
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ ERROR: This script requires Administrator privileges!
    echo Please right-click this batch file and select "Run as Administrator"
    pause
    exit /b 1
)

echo ✅ Administrator privileges detected.
echo.

REM === Configuration ===
set SCRIPT_DIR=%~dp0
set BACKUP_SCRIPT=%SCRIPT_DIR%backup_sqlite_db.bat
set TASK_NAME=ForexExchange_Daily_Backup

REM === Check if backup script exists ===
if not exist "%BACKUP_SCRIPT%" (
    echo ❌ ERROR: Backup script not found!
    echo Expected: %BACKUP_SCRIPT%
    pause
    exit /b 1
)

echo 📋 Configuration:
echo Task Name: %TASK_NAME%
echo Backup Script: %BACKUP_SCRIPT%
echo.

REM === Ask for time preference ===
set /p BACKUP_TIME="Enter backup time (24-hour format, e.g., 02:00 for 2 AM): "

if "%BACKUP_TIME%"=="" (
    set BACKUP_TIME=02:00
    echo Using default time: 02:00
)

echo.
echo ⏱️  Setup Details:
echo  • Task: %TASK_NAME%
echo  • Schedule: Daily at %BACKUP_TIME%
echo  • Script: %BACKUP_SCRIPT%
echo.

set /p CONFIRM="Confirm and create scheduled task? (YES/NO): "

if not "%CONFIRM%"=="YES" (
    echo ⏸️  Setup cancelled.
    pause
    exit /b 0
)

echo.
echo 🔧 Creating scheduled task...
echo.

REM === Create Task Scheduler task ===
schtasks /create /tn "%TASK_NAME%" ^
    /tr "\"%BACKUP_SCRIPT%\"" ^
    /sc daily /st %BACKUP_TIME% ^
    /ru SYSTEM /f /rl highest

if %errorlevel% equ 0 (
    echo.
    echo =====================================================
    echo ✅ Scheduled task created successfully!
    echo =====================================================
    echo.
    echo 📅 Task Details:
    echo  • Name: %TASK_NAME%
    echo  • Frequency: Daily
    echo  • Time: %BACKUP_TIME%
    echo  • Status: Enabled
    echo.
    echo 📁 Backups will be saved to:
    echo  • %SCRIPT_DIR%db_backups\
    echo.
    echo 🔍 View scheduled tasks:
    echo  • Open Task Scheduler (taskmgmt.msc)
    echo  • Look for: %TASK_NAME%
    echo.
) else (
    echo ❌ Failed to create scheduled task!
    echo Please check that you have Administrator privileges.
    pause
    exit /b 1
)

REM === Optional: Show task details ===
echo.
set /p SHOW_TASK="Show task details? (YES/NO): "

if "%SHOW_TASK%"=="YES" (
    echo.
    echo 📋 Task Details:
    schtasks /query /tn "%TASK_NAME%" /v
    echo.
)

echo.
pause
endlocal

