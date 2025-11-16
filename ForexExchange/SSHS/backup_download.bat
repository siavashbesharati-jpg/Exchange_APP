@echo off
setlocal ENABLEDELAYEDEXPANSION
title Backup & Download ForexExchange Database

REM === CONFIGURATION ===
set SERVER=root@104.234.46.151
set DB_PATH=/var/www/taban/ForexExchange.db
set BACKUP_DIR=/var/www/Taban_backUp
set LOCAL_BACKUP_DIR=%~dp0db_backups

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
set BACKUP_FILE=ForexExchange_%DATETIME%.db

echo.
echo =====================================================
echo Backup and Download Database
echo =====================================================
echo.
echo Server: %SERVER%
echo Database: %DB_PATH%
echo Backup Directory: %BACKUP_DIR%
echo Local Save: %LOCAL_BACKUP_DIR%
echo.

REM === Create local directory ===
if not exist "%LOCAL_BACKUP_DIR%" (
    mkdir "%LOCAL_BACKUP_DIR%"
    echo Created: %LOCAL_BACKUP_DIR%
)

if not exist "%LOCAL_BACKUP_DIR%" (
    echo ERROR: Could not create directory: %LOCAL_BACKUP_DIR%
    pause
    exit /b 1
)

REM === STEP 1: Backup on server ===
echo Step 1: Creating backup on server...
ssh %SERVER% "cp %DB_PATH% %BACKUP_DIR%/%BACKUP_FILE%"

if %errorlevel% neq 0 (
    echo ERROR: Backup failed!
    pause
    exit /b 1
)
echo OK: Backup created

REM === STEP 2: Download ===
echo Step 2: Downloading to local computer...
scp %SERVER%:%BACKUP_DIR%/%BACKUP_FILE% "%LOCAL_BACKUP_DIR%\%BACKUP_FILE%"

if %errorlevel% neq 0 (
    echo ERROR: Download failed!
    pause
    exit /b 1
)
echo OK: Downloaded

REM === STEP 3: Verify ===
echo Step 3: Verifying...
if exist "%LOCAL_BACKUP_DIR%\%BACKUP_FILE%" (
    for %%A in ("%LOCAL_BACKUP_DIR%\%BACKUP_FILE%") do (
        set SIZE=%%~zA
    )
    echo.
    echo =====================================================
    echo SUCCESS!
    echo =====================================================
    echo File: %BACKUP_FILE%
    echo Size: %SIZE% bytes
    echo Location: %LOCAL_BACKUP_DIR%
    echo.
) else (
    echo ERROR: Verification failed!
    pause
    exit /b 1
)

pause
endlocal

