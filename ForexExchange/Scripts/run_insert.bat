@echo off
echo 🚀 Starting manual balance history records insertion...

REM Navigate to the project directory
cd /d "E:\IRANEXPEDIA\Exchange_APP\ForexExchange"
if errorlevel 1 (
    echo ❌ Could not navigate to project directory
    exit /b 1
)

echo 📁 Building project...
dotnet build --configuration Release --no-restore --verbosity quiet
if errorlevel 1 (
    echo ❌ Build failed!
    exit /b 1
)

echo ✅ Build successful!

echo 💾 Creating temporary console application for insertion...

REM Create temporary directory
mkdir temp_insert_project 2>nul
cd temp_insert_project

REM Create console application
dotnet new console --force >nul 2>&1
if errorlevel 1 (
    echo ❌ Failed to create console application
    cd ..
    rmdir /s /q temp_insert_project
    exit /b 1
)

REM Copy our insertion script
copy ..\Scripts\InsertManualRecords.cs Program.cs >nul
if errorlevel 1 (
    echo ❌ Failed to copy insertion script
    cd ..
    rmdir /s /q temp_insert_project
    exit /b 1
)

REM Add reference to main project
dotnet add reference ..\ForexExchange.csproj >nul 2>&1

echo 🔧 Running database insertion...
dotnet run

REM Store the exit code
set insertion_result=%errorlevel%

REM Clean up
cd ..
rmdir /s /q temp_insert_project

if %insertion_result% equ 0 (
    echo 🎉 Manual balance history records insertion completed successfully!
) else (
    echo ❌ Insertion failed with error code %insertion_result%
)

exit /b %insertion_result%