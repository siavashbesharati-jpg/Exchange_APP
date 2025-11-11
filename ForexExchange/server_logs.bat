@echo off
REM ==================================================
REM  server_log.bat
REM  Download all .txt logs from remote server folder
REM ==================================================

setlocal enabledelayedexpansion

:: --- CONFIG ---
set SERVER=104.234.46.151
set USER=root
set REMOTE_DIR=/var/www/taban/Logs
set KEYFILE=
::  Example: set KEYFILE=C:\Users\You\.ssh\id_rsa
:: -----------

:: Folder of this script
set "SCRIPT_DIR=%~dp0"
set "DEST=%SCRIPT_DIR%server_logs"

if not exist "%DEST%" mkdir "%DEST%"

echo.
echo ==================================================
echo  Downloading logs from %USER%@%SERVER%:%REMOTE_DIR%
echo  To local folder: %DEST%
echo ==================================================
echo.

if "%KEYFILE%"=="" (
    echo Using password authentication...
    scp -r %USER%@%SERVER%:"%REMOTE_DIR%/*.txt" "%DEST%\"
) else (
    echo Using key authentication: %KEYFILE%
    scp -i "%KEYFILE%" -r %USER%@%SERVER%:"%REMOTE_DIR%/*.txt" "%DEST%\"
)

if %ERRORLEVEL% neq 0 (
    echo.
    echo ❌ SCP failed! Check:
    echo  - Password or key authentication
    echo  - Path exists: %REMOTE_DIR%
    echo  - "scp" available in PATH
    echo.
    pause
    exit /b 1
)

echo.
echo ✅ Logs downloaded successfully to: "%DEST%"
pause
endlocal
