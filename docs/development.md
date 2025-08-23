# Development Guide

## Repository Structure
- ForexExchange/ app: Controllers, Models, Services, Views, wwwroot, Migrations.

## Environment Setup (PowerShell)
```powershell
# Clone
git clone <repository-url>
cd Exchange_APP/ForexExchange

# Build
dotnet restore; dotnet build

# Database
# (Ensure EF tools installed)
dotnet ef database update

# Run
dotnet run
# Open http://localhost:5063
```

## Branching & Commits
- master (prod), develop, feature/*, bugfix/*, hotfix/*, docs/*
- Conventional commits: feat|fix|docs|refactor|test|chore(scope): message

## PR Template (summary)
- Description, Type, Testing, Screenshots, Related Issues.

## Default Login
- admin@iranexpedia.com / Admin123!
