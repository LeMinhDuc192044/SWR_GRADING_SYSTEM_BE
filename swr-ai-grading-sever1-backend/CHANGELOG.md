# Changelog

## Next
- Mã hóa CCCD trong DB (AES-256)
- Refresh token
- Test login với 3 seed accounts (chờ Supabase online)

## 2026-09-09
- Clean Architecture refactor: xóa cấu trúc cũ (1.Domain/2.Application/3.Infrastructure/4.WebAPI), xây mới Domain/Application/Infrastructure/WebApi (@ai)
- Thêm JWT Authentication: BCrypt password hashing, Bearer token, JwtTokenService (@ai)
- Thêm AuthSeeder: 3 seed accounts (Admin/Lecturer/Student) auto-create khi DB trống, cấu hình trong appsettings.json (@ai)
- Bind AuthOptions từ appsettings.json section "Auth" (@ai)
- Sync tất cả entities (User/Examination/Submission/Grading/Semester/ExamMaterial) với Supabase schema (@ai)
- Thêm FluentValidation, Swagger Bearer auth, CORS (@ai)
