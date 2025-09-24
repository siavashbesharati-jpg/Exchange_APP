#!/bin/bash
# Simple execution script for inserting manual records

echo "🚀 Starting manual balance history records insertion..."

# Navigate to the project directory
cd "E:\IRANEXPEDIA\Exchange_APP\ForexExchange" || exit 1

# Compile and run the insertion script
echo "📁 Compiling insertion script..."
dotnet build --configuration Release --no-restore --verbosity quiet

if [ $? -eq 0 ]; then
    echo "✅ Compilation successful!"
    
    echo "💾 Running database insertion..."
    
    # Copy the insertion script to a temporary location and execute it
    cp Scripts/InsertManualRecords.cs temp_insert.cs
    
    # Create a simple console app project for the script
    mkdir -p temp_insert_project
    cd temp_insert_project
    
    echo "Creating temporary console application..."
    dotnet new console --force
    
    # Copy our script over the default Program.cs
    cp ../temp_insert.cs Program.cs
    
    # Add Entity Framework reference
    dotnet add reference ../ForexExchange.csproj
    
    # Run the insertion
    dotnet run
    
    # Clean up
    cd ..
    rm -rf temp_insert_project
    rm temp_insert.cs
    
    echo "🎉 Insertion process completed!"
else
    echo "❌ Compilation failed!"
    exit 1
fi