@echo off
setlocal ENABLEDELAYEDEXPANSION
title Upload & Replace Database

REM === CONFIGURATION ===
set SERVER=root@104.234.46.151
set LOCAL_DB_FILE=%~dp0db_sync\ForexExchange.db
set SERVER_DB_PATH=/var/www/taban/ForexExchange.db
set SERVER_BACKUP_DIR=/var/www/Taban_backUp

REM === Generate timestamp ===
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
set BACKUP_FILE=ForexExchange_BACKUP_%DATETIME%.db

echo.
echo =====================================================
echo Upload and Replace Database
echo =====================================================
echo.
echo Local DB: %LOCAL_DB_FILE%
echo Server DB: %SERVER_DB_PATH%
echo Server Backup: %SERVER_BACKUP_DIR%
echo.

REM === Check local file exists ===
if not exist "%LOCAL_DB_FILE%" (
    echo ERROR: Local database not found!
    echo Expected: %LOCAL_DB_FILE%
    pause
    exit /b 1
)

for %%A in ("%LOCAL_DB_FILE%") do set LOCAL_SIZE=%%~zA
echo Local file size: %LOCAL_SIZE% bytes
echo.

REM === STEP 1: Create backup on server ===
echo Step 1: Creating backup on server...
ssh %SERVER% "cp %SERVER_DB_PATH% %SERVER_BACKUP_DIR%/%BACKUP_FILE%"

if %errorlevel% neq 0 (
    echo ERROR: Backup creation failed!
    pause
    exit /b 1
)
echo OK: Backup created at %SERVER_BACKUP_DIR%/%BACKUP_FILE%
echo.

REM === STEP 2: Upload new database ===
echo Step 2: Uploading new database to server...
scp "%LOCAL_DB_FILE%" %SERVER%:%SERVER_DB_PATH%

if %errorlevel% neq 0 (
    echo ERROR: Upload failed!
    echo Restoring from backup...
    ssh %SERVER% "cp %SERVER_BACKUP_DIR%/%BACKUP_FILE% %SERVER_DB_PATH%"
    echo Database restored from backup.
    pause
    exit /b 1
)
echo OK: Database uploaded
echo.

REM === STEP 3: Verify ===
echo Step 3: Verifying on server...
ssh %SERVER% "ls -lh %SERVER_DB_PATH%"

echo.
echo =====================================================
echo SUCCESS!
echo =====================================================
echo.
echo Database replaced successfully!
echo Backup: %SERVER_BACKUP_DIR%/%BACKUP_FILE%
echo New DB: %SERVER_DB_PATH%
echo.

pause
endlocal

