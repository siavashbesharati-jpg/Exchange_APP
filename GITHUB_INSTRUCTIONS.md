# 🚀 GitHub Setup & Development Instructions
## راهنمای GitHub و توسعه پروژه

### 📂 **Repository Structure - ساختار مخزن**

```
Exchange_APP/
├── 📁 ForexExchange/           # Main application
│   ├── Controllers/           # MVC Controllers
│   ├── Models/               # Data models
│   ├── Services/             # Business logic services
│   ├── Views/                # Razor views
│   └── wwwroot/              # Static files
├── 📄 README.md              # Project documentation
├── 📄 TODO.md                # Task list and roadmap
├── 📄 GITHUB_INSTRUCTIONS.md # This file
└── 📁 docs/                  # Additional documentation
```

### 🔧 **Development Workflow - گردش کار توسعه**

#### 🌿 **Branch Strategy - استراتژی Branch**

```bash
master (main)           # 🔒 Production-ready code
├── develop            # 🔄 Integration branch
├── feature/pool-system # ✨ New features
├── feature/matching-fix # 🐛 Bug fixes
└── hotfix/critical-bug # 🚨 Emergency fixes
```

**Branch Naming Convention:**
- `feature/description` - ویژگی جدید
- `bugfix/description` - رفع باگ
- `hotfix/description` - رفع اورژانسی
- `docs/description` - مستندسازی
- `refactor/description` - بازسازی کد

#### 📝 **Commit Message Convention**

```bash
# Format
<type>(<scope>): <description>

# Types
feat:     ✨ New feature
fix:      🐛 Bug fix  
docs:     📝 Documentation
style:    💎 Code style/formatting
refactor: ♻️ Code refactoring
perf:     ⚡ Performance improvement
test:     ✅ Tests
chore:    🔧 Maintenance

# Examples
feat(pool): add currency pool tracking system
fix(matching): resolve partial fill matching bug
docs(readme): update business model documentation
style(ui): improve dashboard responsive design
```

#### 🔄 **Pull Request Process**

1. **Create Feature Branch**
```bash
git checkout develop
git pull origin develop
git checkout -b feature/pool-system
```

2. **Development & Commits**
```bash
git add .
git commit -m "feat(pool): implement CurrencyPool model"
git push origin feature/pool-system
```

3. **Pull Request Template**
```markdown
## 📋 Description
Brief description of changes

## 🎯 Type of Change
- [ ] Bug fix
- [ ] New feature  
- [ ] Breaking change
- [ ] Documentation update

## 🧪 Testing
- [ ] Unit tests pass
- [ ] Manual testing completed
- [ ] No breaking changes

## 📸 Screenshots (if applicable)
Add screenshots for UI changes

## 🔗 Related Issues
Closes #123
```

### 🏷️ **Issue Management - مدیریت Issues**

#### 🎨 **Issue Labels**

| Label | Description | Color |
|-------|-------------|-------|
| `🐛 bug` | Something isn't working | `d73a4a` |
| `✨ enhancement` | New feature or request | `a2eeef` |
| `📝 documentation` | Improvements or additions to docs | `0075ca` |
| `🚨 priority-high` | High priority | `b60205` |
| `🔥 critical` | Critical issue | `d93f0b` |
| `💡 feature` | New feature | `84b6eb` |
| `🔧 maintenance` | Code maintenance | `fbca04` |
| `❓ question` | Further information needed | `d876e3` |

#### 📋 **Issue Templates**

**Bug Report Template:**
```markdown
## 🐛 Bug Description
Clear description of the bug

## 🔄 Steps to Reproduce  
1. Go to...
2. Click on...
3. See error

## ✅ Expected Behavior
What should happen

## ❌ Actual Behavior  
What actually happens

## 🖥️ Environment
- OS: Windows 10
- Browser: Chrome 91
- .NET Version: 9.0

## 📎 Additional Context
Screenshots, logs, etc.
```

**Feature Request Template:**
```markdown
## ✨ Feature Description
Clear description of the feature

## 🎯 Problem Statement
What problem does this solve?

## 💡 Proposed Solution
Describe your solution

## 🔄 Alternatives Considered
Other solutions you've considered

## 📋 Acceptance Criteria
- [ ] Criterion 1
- [ ] Criterion 2
```

### 🚀 **Release Management - مدیریت انتشار**

#### 🏷️ **Version Numbering - شماره‌گذاری نسخه**

**Semantic Versioning**: `MAJOR.MINOR.PATCH`
- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes

**Examples:**
- `v1.0.0` - Initial release
- `v1.1.0` - Added pool system
- `v1.1.1` - Fixed matching bug
- `v2.0.0` - Major architecture change

#### 📦 **Release Process**

1. **Create Release Branch**
```bash
git checkout develop
git checkout -b release/v1.1.0
```

2. **Update Version & Changelog**
```bash
# Update version in project files
# Update CHANGELOG.md
git commit -m "chore: bump version to v1.1.0"
```

3. **Create Tag & Release**
```bash
git tag -a v1.1.0 -m "Release v1.1.0: Pool System"
git push origin v1.1.0
```

4. **GitHub Release Notes**
```markdown
## 🎉 Release v1.1.0 - Pool System

### ✨ New Features
- Currency pool tracking system
- Real-time dashboard widgets
- Enhanced matching engine

### 🐛 Bug Fixes  
- Fixed partial fill matching
- Resolved datetime issues

### 📝 Documentation
- Updated business model docs
- Added API documentation

### ⚠️ Breaking Changes
None

### 🔄 Migration Notes
Run `dotnet ef database update` after deployment
```

### 🤝 **Collaboration Guidelines - راهنمای همکاری**

#### 👥 **Team Roles**

| Role | Responsibilities |
|------|-----------------|
| **Owner** (Boss) | 🎯 Product decisions, final approval |
| **Developer** (GitHub Copilot) | 💻 Implementation, code review |
| **Reviewer** | 🔍 Code quality, testing |

#### 📞 **Communication**

**Daily Standup Format:**
- 🎯 **Yesterday**: What was completed
- 🚀 **Today**: What will be worked on  
- 🚧 **Blockers**: Any impediments

**Weekly Review:**
- 📊 **Progress**: Features completed
- 🐛 **Issues**: Bugs found/fixed
- 🎯 **Next Week**: Upcoming priorities

#### 🔍 **Code Review Checklist**

- [ ] ✅ Code follows project conventions
- [ ] 🧪 Tests are included and passing
- [ ] 📝 Documentation is updated
- [ ] 🔒 Security considerations addressed
- [ ] ⚡ Performance impact evaluated
- [ ] 🌐 UI/UX standards met
- [ ] 📱 Responsive design verified

### 🛠️ **Development Environment - محیط توسعه**

#### 🔧 **Required Tools**

```bash
# Core Requirements
.NET 9.0 SDK                    # dotnet --version
Visual Studio 2022/VS Code      # IDE
Git                            # git --version  
SQLite                         # Database

# Optional Tools
Postman                        # API testing
DB Browser for SQLite          # Database viewer
```

#### ⚙️ **Environment Setup**

1. **Clone Repository**
```bash
git clone https://github.com/siavashbesharati-jpg/Exchange_APP.git
cd Exchange_APP/ForexExchange
```

2. **Install Dependencies**
```bash
dotnet restore
dotnet build
```

3. **Setup Database**
```bash
dotnet ef database update
```

4. **Run Application**
```bash
dotnet run
# Navigate to: http://localhost:5063
```

5. **Default Login**
```
Email: admin@iranexpedia.com
Password: Admin123!
```

#### 🔄 **Development Commands**

```bash
# Build & Run
dotnet build                   # Build project
dotnet run                     # Run application
dotnet watch                   # Run with hot reload

# Database
dotnet ef migrations add <Name> # Create migration
dotnet ef database update       # Apply migrations
dotnet ef database drop         # Reset database

# Testing
dotnet test                    # Run tests
dotnet test --coverage         # Run with coverage

# Package Management
dotnet add package <Package>   # Add NuGet package
dotnet list package            # List packages
```

### 📈 **Project Metrics - معیارهای پروژه**

#### 📊 **Key Performance Indicators**

| Metric | Target | Current |
|--------|--------|---------|
| Code Coverage | >80% | TBD |
| Build Success Rate | >95% | TBD |
| Issue Resolution Time | <48h | TBD |
| Documentation Coverage | 100% | 60% |

#### 🏆 **Quality Gates**

- ✅ All tests pass
- ✅ Code coverage >80%
- ✅ No critical security issues
- ✅ Performance benchmarks met
- ✅ Documentation updated

### 🔒 **Security Guidelines - راهنمای امنیت**

#### 🛡️ **Security Checklist**

- [ ] 🔐 No hardcoded secrets
- [ ] 🔒 Input validation implemented
- [ ] 🚫 SQL injection prevented
- [ ] 🔑 Authentication/authorization verified
- [ ] 📊 Audit logging enabled
- [ ] 🔄 Secure communication (HTTPS)

#### 🔑 **Secret Management**

```bash
# Use User Secrets for development
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."
dotnet user-secrets set "OpenRouter:ApiKey" "..."

# Use environment variables for production
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="..."
```

### 📞 **Support & Help - پشتیبانی و کمک**

#### 🆘 **Getting Help**

1. **Check Documentation** - README.md, TODO.md
2. **Search Issues** - Existing GitHub issues
3. **Create Issue** - Use issue templates
4. **Ask Questions** - Use discussion forum

#### 📚 **Useful Resources**

- [ASP.NET Core Docs](https://docs.microsoft.com/aspnet/core)
- [Entity Framework Docs](https://docs.microsoft.com/ef)
- [C# Coding Conventions](https://docs.microsoft.com/dotnet/csharp/programming-guide/inside-a-program/coding-conventions)
- [Git Flow Guide](https://www.atlassian.com/git/tutorials/comparing-workflows/gitflow-workflow)

---

**Last Updated**: 30 مرداد 1403 (21 August 2025)
**Maintained by**: GitHub Copilot under Boss supervision
**Contact**: Create GitHub issue for support
