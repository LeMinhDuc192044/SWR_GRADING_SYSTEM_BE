# Backend.Server1 — SWR30x AI-Assisted Grading System

> **Server 1** (repo này): Giảng viên + Khảo thí + AI grading.
> Server 2 (do thành viên khác phụ trách): Sinh viên thi offline.

---

## Mục lục

1. [Bối cảnh](#bối-cảnh)
2. [Công nghệ](#công-nghệ)
3. [Cấu trúc thư mục](#cấu-trúc-thư-mục)
4. [Cài đặt & Chạy](#cài-đặt--chạy)
5. [Cấu hình](#cấu-hình)
6. [Database & Migrations](#database--migrations)
7. [API Endpoints](#api-endpoints)
8. [Rule quan trọng cho AI Agent](#rule-quan-trọng-cho-ai-agent)

---

## Bối cảnh

Hệ thống chấm bài thực hành SWR30x có hỗ trợ AI, gồm **2 server**:

- **Server 1** (repo này): Giảng viên tạo đề, khảo thí kiểm tra, nhận bài chấm AI
- **Server 2**: Sinh viên thi (offline)

**Flow 1 hiện tại:** Register → Login → Semester → Subject → SubjectOffering → Exam → Assign Lecturer → Upload Files → Validate → Submit → Review → Lock → Package → Transfer

---

## Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Framework | ASP.NET Core 8.0 (C#) |
| ORM | Entity Framework Core 8 |
| Database | PostgreSQL (Supabase) |
| Auth | JWT Bearer |
| API Docs | Swagger / OpenAPI 3.0 |
| Validation | FluentValidation |
| Mapping | AutoMapper |
| Architecture | Clean Architecture (4-layer) |
| Testing | xUnit + Moq |

---

## Cấu trúc thư mục

```
Backend.Server1/
├── Backend.Server1.sln
├── README.md
├── RULE.md                              ← LUẬT BẮT BUỘC cho AI agent
├── CONTRIBUTING.md
├── SECRET_MANAGEMENT.md
├── .env.example                         ← Template (KHÔNG có secret)
├── .env                                  ← Local (đã .gitignore, KHÔNG push)
├── .gitignore
│
├── src/
│   ├── Domain/                          ← Entities, Enums, Interfaces — KHÔNG phụ thuộc gì
│   │   ├── Domain.csproj
│   │   ├── Entities/
│   │   │   ├── User.cs
│   │   │   ├── Semester.cs
│   │   │   ├── Examination.cs
│   │   │   ├── ExamMaterial.cs
│   │   │   ├── Submission.cs
│   │   │   └── Grading.cs
│   │   └── Enums/
│   │       ├── SemesterStatus.cs
│   │       ├── Statuses.cs
│   │       └── UserRoleEnum.cs
│   │
│   ├── Application/                     ← DTOs, Services, Validators, Interfaces → Domain
│   │   └── Application.csproj
│   │
│   ├── Infrastructure/                  ← EF Core, Repositories, Storage, External → Application
│   │   ├── Infrastructure.csproj
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── AppDbContextDesignFactory.cs
│   │   │   └── Configurations/
│   │   │       ├── UserConfiguration.cs
│   │   │       ├── SemesterConfiguration.cs
│   │   │       ├── ExaminationConfiguration.cs
│   │   │       ├── ExamMaterialConfiguration.cs
│   │   │       ├── SubmissionConfiguration.cs
│   │   │       └── GradingConfiguration.cs
│   │   └── Migrations/
│   │
│   └── WebApi/                          ← Controllers, Middleware, Program.cs → Infrastructure
│       ├── WebApi.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Controllers/
│       │   └── HealthController.cs
│       └── Middleware/
│
└── tests/
    ├── Domain.UnitTests/                ← Unit test cho Domain
    │   └── Domain.UnitTests.csproj
    └── Application.UnitTests/           ← Unit test cho Application
        └── Application.UnitTests.csproj
```

**Quy tắc phụ thuộc (Dependency Rule):**
```
WebApi  →  Infrastructure  →  Application  →  Domain
   ↑                              ↑              ↑
   (điểm vào)               (use cases)    (entities, enums)
```
**Domain KHÔNG được phụ thuộc bất kỳ project nào khác.**

---

## Cài đặt & Chạy

### Yêu cầu

- .NET 8.0 SDK
- Supabase project (Postgres + connection string)

### Các bước

```powershell
# 1. Mở thư mục
cd D:\FPT\2025_k9\DoAn\Backend_Server1

# 2. Cấu hình .env
copy .env.example .env
# Sửa .env: điền ConnectionStrings__DefaultConnection (Supabase), JWT_SECRET, ...

# 3. Restore + Build
dotnet restore
dotnet build

# 4. Apply migrations lên Supabase
dotnet ef database update --project src/Infrastructure --startup-project src/WebApi

# 5. Chạy WebApi
dotnet run --project src/WebApi

# 6. Mở Swagger
# http://localhost:5000/swagger
```

### Chạy tests

```powershell
dotnet test
```

---

## Cấu hình

Tất cả cấu hình qua **biến môi trường** (load từ `.env` bằng DotNetEnv).

**QUAN TRỌNG — KHÔNG BAO GIỜ hardcode secret trong code hay appsettings.json.**

Xem chi tiết từng biến trong `.env.example`. Các biến chính:

| Biến | Bắt buộc | Mô tả |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | ✅ | Npgsql connection string tới Supabase |
| `JWT_SECRET` | ✅ | Min 32 ký tự, dùng để ký JWT |
| `JWT_ISSUER` | ✅ | Issuer trong JWT claim |
| `JWT_AUDIENCE` | ✅ | Audience trong JWT claim |
| `JWT_EXPIRY_MINUTES` | ⬜ | Mặc định 60 |
| `ASPNETCORE_ENVIRONMENT` | ⬜ | `Development` / `Production` |
| `ASPNETCORE_URLS` | ⬜ | Mặc định `http://localhost:5000` |
| `CORS_ALLOWED_ORIGINS` | ⬜ | Danh sách URL, phân cách dấu phẩy |

---

## Database & Migrations

### Schema Supabase là CHUẨN

Schema `public.*` trên Supabase đã được chốt và **KHÔNG được tự ý thay đổi**.
Nếu cần thay đổi schema, phải có yêu cầu từ người phụ trách database.

**Quy trình thay đổi schema:**
1. Tạo migration SQL mới trong folder `src/Infrastructure/Migrations/`
2. EF migration sẽ generate file `*.cs` tương ứng
3. **KHÔNG tự apply** lên database — chờ user xác nhận

### Các lệnh EF thường dùng

```powershell
# Generate migration mới
dotnet ef migrations add <TenMigration> --project src/Infrastructure --startup-project src/WebApi

# Apply migration lên Supabase (chỉ chạy khi user yêu cầu)
dotnet ef database update --project src/Infrastructure --startup-project src/WebApi

# Xem danh sách migration
dotnet ef migrations list --project src/Infrastructure --startup-project src/WebApi

# Tạo SQL script (không apply)
dotnet ef migrations script --project src/Infrastructure --startup-project src/WebApi --output migration.sql

# Rollback migration (cẩn thận — có thể mất data)
dotnet ef database update <PreviousMigrationName> --project src/Infrastructure --startup-project src/WebApi
```

### Bảng hiện có trên Supabase

| Bảng | Mô tả |
|---|---|
| `user` | Người dùng (student/lecturer/admin) |
| `semester` | Học kỳ |
| `examination` | Kỳ thi / Đợt thi |
| `exam_material` | Đề thi / Tài liệu thi |
| `submission` | Bài nộp của sinh viên |
| `grading` | Kết quả chấm điểm |

---

## API Endpoints

### Hiện có

| Method | URL | Mô tả | Auth |
|---|---|---|---|
| GET | `/health` | Health check | ❌ |
| GET | `/api/health` | Health check (controller) | ❌ |
| GET | `/swagger` | Swagger UI (chỉ Development) | ❌ |

### Sắp thêm (theo Flow 1)

| Module | Endpoints |
|---|---|
| Auth | `/api/auth/register`, `/api/auth/login`, `/api/auth/me` |
| Users | `/api/users` (CRUD) |
| Semesters | `/api/semesters` (CRUD) |
| Examinations | `/api/examinations` (CRUD) |
| ExamMaterials | `/api/examinations/{id}/materials` (upload/list) |
| Submissions | `/api/submissions` (CRUD, submit) |
| Gradings | `/api/gradings` (CRUD) |

---

## Rule quan trọng cho AI Agent

> **BẮT BUỘC đọc [`RULE.md`](./RULE.md) trước khi code bất kỳ thứ gì.**

Tóm tắt nhanh:
1. **KHÔNG ĐẶT SỐ** ở đầu tên folder/project (KHÔNG dùng `1.Domain`, `2.Application`...)
2. **KHÔNG TỰ Ý SỬA SCHEMA SUPABASE** — phải có yêu cầu từ user
3. **Mọi request phải có DOC** — cập nhật README.md hoặc RULE.md sau khi xong
4. **Đa vai (multi-role)** — mỗi task phức tạp phải xác định rõ role chịu trách nhiệm

---

## Tài liệu liên quan

| File | Mô tả |
|---|---|
| [`RULE.md`](./RULE.md) | Luật bắt buộc cho AI agent |
| [`CONTRIBUTING.md`](./CONTRIBUTING.md) | Quy ước đóng góp |
| [`SECRET_MANAGEMENT.md`](./SECRET_MANAGEMENT.md) | Quản lý secret |
| `../doc/` | Tài liệu dự án (context, plan, decisions, architecture) |

---

## Trạng thái dự án

| Hạng mục | Trạng thái |
|---|---|
| Solution + 4 projects + 2 test projects | ✅ |
| Entities + EF Configurations | ✅ |
| AppDbContext + JWT + Swagger setup | ✅ |
| Migration `InitialSchema` (generated) | ✅ |
| Apply migration lên Supabase | ⏳ Chờ user test |
| Auth module (Register/Login) | ⏳ Chưa làm |
| Semester/Examination/Submission/Grading CRUD | ⏳ Chưa làm |
| Unit tests | ⏳ Chưa có test case cụ thể |
