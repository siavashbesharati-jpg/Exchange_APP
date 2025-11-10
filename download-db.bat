@echo off
setlocal enabledelayedexpansion

REM ============================================
REM Download Database File Script
REM ============================================

REM Configuration
set SERVER_IP=104.234.46.151
set SERVER_USER=root
set SERVER_PATH=/var/www/taban
set DB_FILE=ForexExchange.db
set LOCAL_PATH=.

REM SSH Options
set SSH_OPTS=-o ConnectTimeout=10 -o BatchMode=yes -o StrictHostKeyChecking=no -o UserKnownHostsFile=NUL

REM Colors for output
set RESET=[0m
set GREEN=[32m
set YELLOW=[33m
set RED=[31m
set BLUE=[34m
set RESET_VAR=%RESET%

echo.
echo %BLUE%==============================================%RESET%
echo %BLUE%  Download Database File Script%RESET%
echo %BLUE%==============================================%RESET%
echo.

REM Step 1: Check if file exists on server
echo %YELLOW%[1/3] Checking if file exists on server...!RESET_VAR!
ssh %SSH_OPTS% %SERVER_USER%@%SERVER_IP% "test -f %SERVER_PATH%/%DB_FILE% && echo 'File exists' || echo 'File not found'"
if %ERRORLEVEL% NEQ 0 (
    echo %RED%Error: Failed to check file on server!RESET_VAR!
    exit /b 1
)
echo %GREEN%[OK] File check completed!RESET_VAR!
echo.

REM Step 2: Download the file
echo %YELLOW%[2/3] Downloading %DB_FILE%...!RESET_VAR!
scp -o ConnectTimeout=10 -o BatchMode=yes -o StrictHostKeyChecking=no -o UserKnownHostsFile=NUL %SERVER_USER%@%SERVER_IP%:%SERVER_PATH%/%DB_FILE% %LOCAL_PATH%/
if %ERRORLEVEL% NEQ 0 (
    echo %RED%Error: Failed to download file!RESET_VAR!
    echo %RED%Please check:!RESET_VAR!
    echo %RED%  1. SSH connection and credentials!RESET_VAR!
    echo %RED%  2. File permissions on server!RESET_VAR!
    echo %RED%  3. File path: %SERVER_PATH%/%DB_FILE%!RESET_VAR!
    exit /b 1
)
echo %GREEN%[OK] File downloaded successfully!RESET_VAR!
echo.

REM Step 3: Verify downloaded file
echo %YELLOW%[3/3] Verifying downloaded file...!RESET_VAR!
if exist "%LOCAL_PATH%\%DB_FILE%" (
    for %%A in ("%LOCAL_PATH%\%DB_FILE%") do (
        set FILE_SIZE=%%~zA
        echo %GREEN%[OK] File downloaded: %DB_FILE%!RESET_VAR!
        echo %BLUE%  File size: !FILE_SIZE! bytes!RESET_VAR!
    )
) else (
    echo %RED%Error: Downloaded file not found!RESET_VAR!
    exit /b 1
)
echo.

echo %GREEN%==============================================%RESET%
echo %GREEN%  Download completed successfully!%RESET%
echo %GREEN%==============================================%RESET%
echo.
echo %BLUE%File location: %LOCAL_PATH%\%DB_FILE%!RESET_VAR!
echo.

endlocal

