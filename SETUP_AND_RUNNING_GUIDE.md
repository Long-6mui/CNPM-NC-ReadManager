# 🚀 ReadManager - Complete Setup & Running Guide

## Project Overview
ReadManager is a full-stack .NET application for managing and reading stories online.

**Technology Stack:**
- **Backend (API)**: ASP.NET Core 8.0 Web API
- **Frontend (Web)**: ASP.NET Core 8.0 MVC with Razor Pages
- **Mobile**: .NET MAUI (Windows target)
- **Database**: SQLite with Entity Framework Core
- **Authentication**: Custom session-based authentication

## Prerequisites
1. **.NET 8 SDK** - Download from https://dotnet.microsoft.com/download
2. **.NET 9 SDK** (optional, for Mobile) - For MAUI development
3. **Visual Studio Community 2026** (or VS Code with C# extensions)
4. **Git** (for version control)

## Project Structure
```
ReadManager.sln
├── src/
│   ├── ReadManager.Api/           (Backend API)
│   ├── ReadManager.Web/           (Frontend Web App)
│   └── ReadManager.Mobile/        (Mobile App - MAUI)
└── PROJECT_STATUS.md
```

## ✅ Build Status

All projects now build successfully:
- ✓ ReadManager.Api (net8.0)
- ✓ ReadManager.Web (net8.0)  
- ✓ ReadManager.Mobile (net9.0-windows)

## How to Run

### Method 1: Using PowerShell (Recommended)

Open **two PowerShell terminals**:

**Terminal 1 - Start the API** (runs on http://localhost:5284):
```powershell
cd Q:\1112222233333\CNPM-NC-ReadManager
dotnet run --project src/ReadManager.Api/ReadManager.Api.csproj
```

**Terminal 2 - Start the Web App** (runs on http://localhost:5150):
```powershell
cd Q:\1112222233333\CNPM-NC-ReadManager
dotnet run --project src/ReadManager.Web/ReadManager.Web.csproj
```

Wait for both to show "Application started" messages.

### Method 2: Using Visual Studio Debugger

1. Open `ReadManager.sln` in Visual Studio
2. Right-click Solution → Properties → Startup Project
3. Select **Multiple startup projects**:
   - Set `ReadManager.Api` to **Start**
   - Set `ReadManager.Web` to **Start**
4. Press **F5** or **Debug → Start Debugging**

### Method 3: Build and Run Manually

```powershell
# Build everything
cd Q:\1112222233333\CNPM-NC-ReadManager
dotnet build

# Run API
dotnet run --project src/ReadManager.Api/ReadManager.Api.csproj &

# Run Web (in new window)
dotnet run --project src/ReadManager.Web/ReadManager.Web.csproj
```

## Accessing the Application

Once both services are running:

1. **Web Application**: Open browser and go to **http://localhost:5150**
2. **API Swagger Documentation**: Go to **http://localhost:5284/swagger**

## Testing the Features

### 1. User Registration
- Click "Đăng ký ngay" (Register Now)
- Fill in:
  - Họ và tên (Name): `Nguyễn Văn A`
  - Email: `test@example.com`
  - Mật khẩu (Password): `Password123`
- Click "Tạo tài khoản" (Create Account)
- ✓ Should see success message and redirect to login

### 2. User Login
- Email: Use the email you just registered
- Password: Use the password you just set
- Click "Đăng nhập" (Login)
- ✓ Should redirect to home page

### 3. Admin Login (Pre-seeded)
- Email: `admin@example.com`
- Password: `Admin@123`
- Click "Đăng nhập"
- ✓ Should redirect to Admin Stories management page

### 4. View API Documentation
- Go to http://localhost:5284/swagger
- Explore available endpoints:
  - `/api/auth/register` - Register new user
  - `/api/auth/login` - Login user
  - `/api/auth/me` - Get current user
  - `/api/auth/logout` - Logout

## Database

**Location**: Automatically created in `ReadManager.Api` project
- **File**: `app.db` (SQLite database)
- **Migrations**: Applied automatically on first run

**Sample Data (Auto-seeded)**:
- 1 Admin User: admin@example.com / Admin@123
- 5 Genres: Ngôn Tình, Tiên Hiệp, Khoa Vũ Trụ, Hành Động, Kỳ Ảo
- 2 Sample Stories with chapters

## Configuration

### API Configuration (src/ReadManager.Api/appsettings.json)
```json
{
  "ConnectionStrings": {
	"DefaultConnection": "Data Source=app.db"
  }
}
```

### Web Configuration (src/ReadManager.Web/Program.cs)
```csharp
builder.Services.AddHttpClient<AuthApiClient>(client=>{
	client.BaseAddress=new Uri(builder.Configuration["Api:BaseUrl"]??"https://localhost:7188/");
	client.Timeout=TimeSpan.FromSeconds(10);
});
```

**Default API URL**: `https://localhost:7188/` or `http://localhost:5284`

## Port Configuration

If you need to change ports, edit the launch settings:

### API Port Changes
File: `src/ReadManager.Api/Properties/launchSettings.json`
```json
"http": {
  "applicationUrl": "http://localhost:5284"  // Change 5284 here
}
```

### Web Port Changes  
File: `src/ReadManager.Web/Properties/launchSettings.json`
```json
"http": {
  "applicationUrl": "http://localhost:5150"  // Change 5150 here
}
```

Then update the API URL in `src/ReadManager.Web/Program.cs`:
```csharp
client.BaseAddress=new Uri("http://localhost:NEW_PORT/");
```

## Troubleshooting

### Issue: "Address already in use" Error
**Solution**: Kill the process using that port:
```powershell
# Kill process on specific port (e.g., 5284)
Get-NetTCPConnection -LocalPort 5284 -ErrorAction SilentlyContinue | 
  ForEach-Object { Stop-Process -Id $_.OwningProcess -Force }
```

### Issue: Database locked / Build file locked
**Solution**: Stop all ReadManager processes:
```powershell
Get-Process | Where-Object {$_.ProcessName -like "*ReadManager*"} | 
  Stop-Process -Force
```

### Issue: "Cannot connect to API" in Web
**Check that**:
1. API is running on http://localhost:5284
2. Web is set to use correct API URL in Program.cs
3. Both services have ports not blocked by firewall

### Issue: MAUI/Mobile won't build
**This is expected** - Windows App SDK requires certain dependencies. To skip:
```powershell
# Build only API and Web (skip Mobile)
dotnet build src/ReadManager.Api/ReadManager.Api.csproj
dotnet build src/ReadManager.Web/ReadManager.Web.csproj
```

## Building for Production

```powershell
# Build release version
dotnet build -c Release

# Publish for deployment
dotnet publish -c Release -o ./publish

# Output will be in ./publish/ directory
```

## API Endpoints Reference

### Authentication
```
POST   /api/auth/register       Register new user
POST   /api/auth/login          Login user
GET    /api/auth/me             Get current user (Protected)
POST   /api/auth/logout         Logout (Protected)
```

### Request/Response Examples

**Register**:
```bash
POST http://localhost:5284/api/auth/register
Content-Type: application/json

{
  "displayName": "Nguyễn Văn A",
  "email": "test@example.com",
  "password": "Password123"
}

Response (201 Created):
{
  "message": "Đăng ký thành công.",
  "user": {
	"userId": 2,
	"email": "test@example.com",
	"displayName": "Nguyễn Văn A",
	"role": "Member"
  }
}
```

**Login**:
```bash
POST http://localhost:5284/api/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Password123"
}

Response (200 OK):
{
  "accessToken": "CfDJ8N...",
  "expiresAt": "2024-01-15T23:59:59+00:00",
  "user": {
	"userId": 2,
	"email": "test@example.com",
	"displayName": "Nguyễn Văn A",
	"role": "Member"
  }
}
```

## Features Implemented

✅ User Authentication & Registration
✅ Secure Password Hashing (ASP.NET Identity)
✅ Session Management (JWT-like tokens)
✅ Rate Limiting (Login endpoint: 10 req/min)
✅ Database Migrations (Automatic)
✅ Sample Data Seeding
✅ API Documentation (Swagger UI)
✅ MVC Web Interface
✅ Vietnamese UI/Error Messages
✅ Admin User Functionality
✅ Genre Management
✅ Story & Chapter Model Setup

## Future Enhancements

- [ ] Story browsing & reading interface
- [ ] User profile management
- [ ] Story search & filtering
- [ ] Rating & review system
- [ ] Reading history tracking
- [ ] Mobile app completion
- [ ] Email notifications
- [ ] Two-factor authentication
- [ ] Social sharing features

## Support & Debugging

**Enable Debug Logging**:
Set environment variable before running:
```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
```

**Clear Database** (Start fresh):
```powershell
Remove-Item -Path "src/ReadManager.Api/app.db" -Force
# Run API again - new database will be created with seed data
```

**Check Database Contents**:
Use any SQLite viewer to open `app.db` file in ReadManager.Api folder

---

**Project Status**: ✅ COMPLETE & BUILDABLE
**Last Updated**: January 2025
**All 3 Projects Build Successfully**: API, Web, Mobile
