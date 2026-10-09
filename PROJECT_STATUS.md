# ReadManager Project - Status Report

## ✅ COMPLETED FEATURES

### Backend (ReadManager.Api) - FULLY FUNCTIONAL
- **Authentication System**
  - User Registration: `POST /api/auth/register`
  - User Login: `POST /api/auth/login`
  - User Profile: `GET /api/auth/me` (Protected)
  - User Logout: `POST /api/auth/logout` (Protected)
  - Password Hashing with ASP.NET Identity
  - Rate Limiting (10 requests per minute on login)

- **Database**
  - SQLite with Entity Framework Core
  - Automatic migrations on startup
  - Sample data seeding (Admin user, Genres, Stories, Chapters)

- **Services**
  - IStoryService - Story management
  - IGenreService - Genre management

- **Security**
  - API Session Handler (Custom authentication scheme)
  - Data Protection configured
  - Authorization policies in place

### Frontend (ReadManager.Web) - FULLY FUNCTIONAL
- **Authentication Pages**
  - Login page with form validation
  - Registration page with email/password validation
  - Access Denied page
  - Logout functionality

- **API Integration**
  - AuthApiClient for API communication
  - Proper error handling and user messaging (Vietnamese)
  - TempData for success message persistence
  - Session management with JWT-like tokens

- **Features Implemented**
  - User can register new account
  - User can login with credentials
  - User sessions persist with cookies
  - Automatic redirects based on user role (Admin → Admin area, Member → Home)
  - Account navigation in UI

## 🔧 HOW TO RUN

### Prerequisites
- .NET 8 SDK installed
- Visual Studio Community 2026 (or any .NET IDE)

### Start the applications:

1. **Terminal 1 - Run API** (on port 5284):
   ```powershell
   dotnet run --project src/ReadManager.Api/ReadManager.Api.csproj
   ```

2. **Terminal 2 - Run Web** (on port 5150):
   ```powershell
   dotnet run --project src/ReadManager.Web/ReadManager.Web.csproj
   ```

3. **Access the application**:
   - Web: `http://localhost:5150`
   - API Swagger: `http://localhost:5284/swagger`

### Test Flow:
1. Click "Đăng ký ngay" (Register) link
2. Fill in Name, Email, Password
3. Click "Tạo tài khoản" (Create Account)
4. See success message "Đăng ký thành công"
5. Log in with your credentials
6. Redirected to Home page (or Admin area if admin account)

## ⚠️ KNOWN ISSUES

### Mobile Project (ReadManager.Mobile)
- **Status**: Not building due to MAUI/Windows App SDK C++ dependencies not found
- **Error**: `MSB4018: The "GetLatestMSVCVersion" task failed unexpectedly`
- **Cause**: Missing MSVC (Visual Studio C++ tools) installation required by Windows App SDK
- **Fix Options**:
  1. Install C++ development tools in Visual Studio
  2. Set `WindowsAppSDKSelfContained=true` in project file
  3. Or temporarily exclude Mobile project from solution build

### Build Warnings
- Various NuGet package resolution warnings (non-critical)
- These don't affect functionality

## 📋 DATABASE SCHEMA

### Users Table
- UserId (int, PK)
- Username (string, unique)
- Email (string, unique)
- DisplayName (string)
- PasswordHash (string)
- Role (string: Admin/Member)
- AccountStatus (string: Active/Inactive)
- SecurityVersion (int)

### Genres Table
- GenreId (int, PK)
- Name (string)
- Slug (string)

### Stories Table
- StoryId (int, PK)
- Title (string)
- Slug (string)
- AuthorName (string)
- Synopsis (text)
- PublicationStatus (string)
- Visibility (string)
- AccessPolicy (string)
- CreatedBy (int, FK to Users)
- CreatedAt (datetime)
- UpdatedAt (datetime)
- FirstPublishedAt (datetime)

### Chapters Table
- ChapterId (int, PK)
- StoryId (int, FK)
- ChapterNumber (int)
- Title (string)
- Content (text)
- AccessLevel (string)
- PublicationStatus (string)
- PublishedAt (datetime)
- CreatedAt (datetime)
- UpdatedAt (datetime)

## 🚀 NEXT STEPS (Optional Enhancements)

1. **Stories Management UI**
   - Create page for browsing stories
   - Story detail page with chapters
   - Reading interface

2. **User Profile Management**
   - View/Edit user profile
   - Change password
   - Reading history

3. **Admin Panel**
   - Story management (create/edit/delete)
   - User management
   - Genre management
   - Analytics/Reports

4. **Mobile App**
   - Fix MAUI dependencies
   - Implement same auth flow in mobile
   - Responsive UI for mobile reading

5. **Additional Features**
   - Comment system
   - Ratings/Reviews
   - User favorites/bookmarks
   - Full-text search
   - Recommendations

## 📝 SAMPLE CREDENTIALS

Admin account created during seed:
- Email: admin@example.com
- Password: Admin@123

## ✨ API ENDPOINTS AVAILABLE

```
POST   /api/auth/register       - Register new user
POST   /api/auth/login          - Login user
GET    /api/auth/me             - Get current user (Protected)
POST   /api/auth/logout         - Logout user (Protected)
```

**Status**: All core features working. API and Web build successfully and can run independently.
