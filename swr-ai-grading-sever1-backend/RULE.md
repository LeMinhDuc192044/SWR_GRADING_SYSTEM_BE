# RULE.md — Luật bắt buộc cho AI Agent làm việc với Backend.Server1

> **MỤC ĐÍCH:** Tài liệu này ghi lại các luật BẮT BUỘC để tránh lặp lại sai lầm.
> Mọi AI agent (Cursor, Claude Code, GitHub Copilot, v.v.) **PHẢI đọc file này trước khi code**.
>
> **Cập nhật lần cuối:** 2026-09-09 (sau sự cố đặt số 1.2.3.4 ở tên folder)

---

## Mục lục

1. [Nguyên tắc tối thượng](#1-nguyên-tắc-tối-thượng)
2. [Quy ước đặt tên](#2-quy-ước-đặt-tên)
3. [Quy tắc Database / Schema](#3-quy-tắc-database--schema)
4. [Quy tắc sau MỖI request](#4-quy-tắc-sau-mỗi-request)
5. [Đa vai (Multi-role)](#5-đa-vai-multi-role)
6. [Các lỗi đã mắc & cách tránh](#6-các-lỗi-đã-mắc--cách-tránh)

---

## 1. Nguyên tắc tối thượng

| # | Nguyên tắc | Hậu quả nếu vi phạm |
|---|---|---|
| **R1.1** | **KHÔNG BAO GIỜ tự ý thay đổi cấu trúc/đặt tên khi user chưa yêu cầu** | Mất công revert, mất niềm tin user |
| **R1.2** | **KHÔNG BAO GIỜ tự ý sửa schema database** | Phá vỡ production, mất data |
| **R1.3** | **KHÔNG BAO GIỜ hardcode secret** vào code hay appsettings.json | Lộ thông tin nhạy cảm |
| **R1.4** | **KHÔNG BAO GIỜ commit `.env`** lên git | Lộ database password, JWT secret |
| **R1.5** | **Mọi request phải kết thúc bằng việc cập nhật tài liệu** (README, RULE, doc/) | Mất context cho lần sau |

---

## 2. Quy tắc đặt tên

### R2.1 — Folder & Project

**TUYỆT ĐỐI KHÔNG đặt số ở đầu tên folder/project.**

```
❌ SAI:
  1.Domain/
  2.Application/
  3.Infrastructure/
  4.WebAPI/
  1.Domain.UnitTests/

✅ ĐÚNG (chuẩn công ty):
  Domain/
  Application/
  Infrastructure/
  WebApi/
  Domain.UnitTests/
  Application.UnitTests/
```

**Lý do:** Trong công ty thật, không có ai đặt tên folder là `1.Foo`, `2.Bar`. Số ở đầu tên là anti-pattern. Thứ tự được đảm bảo bằng **dependency**, không phải bằng số.

### R2.2 — Namespace & AssemblyName

- `Namespace` phải khớp với folder path (relative tới `src/` hoặc `tests/`).
- `AssemblyName` phải khớp với tên project (không kèm số).
- `RootNamespace` phải khớp với namespace gốc.

**Ví dụ đúng cho `src/WebApi/Controllers/HealthController.cs`:**
```csharp
namespace WebApi.Controllers;
```
**Ví dụ đúng cho `src/Domain/Entities/User.cs`:**
```csharp
namespace Domain.Entities;
```

### R2.3 — Class & Method

- PascalCase cho class, method, property.
- camelCase cho biến local, parameter.
- Tiền tố interface: `I` (VD: `IUserRepository`).
- Tiền tố async method: `Async` (VD: `GetUserAsync`).

---

## 3. Quy tắc Database / Schema

### R3.1 — Schema Supabase là CHUẨN

> **TUYỆT ĐỐI KHÔNG tự ý sửa schema trên Supabase trừ khi user yêu cầu rõ ràng.**

Schema đã có trên Supabase (các bảng `public.*`) được chốt và sử dụng làm source of truth.
Mọi thay đổi phải qua workflow:

1. User yêu cầu thay đổi cụ thể (ví dụ: "thêm cột X vào bảng Y")
2. AI agent tạo EF migration tương ứng
3. AI agent generate SQL script, **KHÔNG tự apply**
4. User review + apply lên Supabase thủ công (qua Supabase SQL Editor hoặc psql)

### R3.2 — Mapping entity ↔ table

- **Tên bảng:** snake_case trong DB, PascalCase trong code
  - DB: `user`, `exam_material` → Code: `User`, `ExamMaterial`
- **Tên cột:** snake_case trong DB → PascalCase trong code
  - DB: `full_name`, `created_at` → Code: `FullName`, `CreatedAt`
- **Lưu ý đặc biệt:** Bảng `user` phải dùng `ToTable("user", ...)` (có thể cần escape vì `user` là keyword trong Postgres)

### R3.3 — Không xóa migration đã apply

- Migration đã apply lên database **KHÔNG ĐƯỢC XÓA**.
- Nếu muốn rollback, tạo migration mới để "đảo ngược" thay vì xóa file cũ.

---

## 4. Quy tắc sau MỖI request

> **Sau mỗi request từ user, AI agent PHẢI:**

### R4.1 — Cập nhật tài liệu
Nếu request thay đổi cấu trúc, naming, workflow, hoặc quyết định kỹ thuật:
- Cập nhật `README.md` (nếu liên quan tới cách dùng)
- Cập nhật `RULE.md` (nếu liên quan tới luật)
- Cập nhật `doc/` (nếu có)

### R4.2 — Build + Test
- Chạy `dotnet build` và đảm bảo **0 error**.
- Nếu thêm test case → chạy `dotnet test`.

### R4.3 — Báo cáo lại user
Sau khi xong, **tóm tắt lại cho user** biết:
- Đã làm gì
- File nào thay đổi
- Có cần user test gì không
- Có cảnh báo gì không

---

## 5. Đa vai (Multi-role)

> **Với task phức tạp, AI agent phải xác định rõ role chịu trách nhiệm.**

### R5.1 — Các role thường gặp

| Role | Trách nhiệm | Khi nào dùng |
|---|---|---|
| **Solution Architect** | Quyết định cấu trúc solution, naming convention, kiến trúc | Setup project mới, refactor lớn |
| **Backend Lead** | Implement entities, EF Core, API endpoints, business logic | Viết code backend |
| **DevOps Engineer** | CI/CD, deployment, secret management, git | Setup pipeline, Docker, deploy |
| **Database Admin (DBA)** | Quản lý schema, migration, performance tuning | Thay đổi schema, optimize query |
| **Tech Writer** | Viết/cập nhật README, RULE, doc/ | Sau mỗi thay đổi đáng kể |
| **QA Engineer** | Viết test case, manual test, bug report | Test feature mới |
| **Security Engineer** | Review code về bảo mật, JWT, secret | Setup auth, xử lý secret |

### R5.2 — Cách áp dụng

**Ví dụ — Request "Tạo lại solution từ đầu":**

1. **Solution Architect:** "Đặt tên Domain/Application/Infrastructure/WebApi theo chuẩn công ty, không dùng số"
2. **Backend Lead:** Tạo 4 project + references + NuGet packages + entities + EF + DI
3. **DevOps Engineer:** Tạo `.gitignore`, `.env.example`, setup `.env` management
4. **Tech Writer:** Viết `README.md` mới + cập nhật `RULE.md`
5. **QA Engineer:** Build + test + báo cáo user

**Trong response cho user, AI agent nên nói rõ:**
> "Em đóng vai **Solution Architect** để quyết định naming, **Backend Lead** để viết code, **Tech Writer** để viết doc."

---

## 6. Các lỗi đã mắc & cách tránh

### Lỗi #1 — Đặt số ở đầu folder/project (2026-09-09)

**Sự cố:**
- AI tự ý đặt tên `1.Domain`, `2.Application`, `3.Infrastructure`, `4.WebAPI`
- User đã OK với cấu trúc này (ban đầu) → nhưng sau đó yêu cầu đổi lại về tên chuẩn công ty
- Phải xóa và làm lại từ đầu

**Nguyên nhân:**
- AI không hiểu convention chuẩn công ty thật
- AI không xác nhận lại convention với user trước khi code

**Cách tránh:**
- ✅ **LUÔN hỏi user** trước khi quyết định naming convention quan trọng
- ✅ Tham khảo `RULE.md` mục 2 trước khi tạo project mới
- ✅ Mặc định dùng tên KHÔNG CÓ SỐ trừ khi user yêu cầu rõ

### Lỗi #2 — Không cập nhật doc sau khi thay đổi (2026-09-09)

**Sự cố:**
- AI thay đổi cấu trúc, generate migration, refactor code
- **KHÔNG cập nhật README.md** để phản ánh thay đổi
- User phát hiện và yêu cầu: "sau mỗi request, bạn cần phải ghi lại doc"

**Cách tránh:**
- ✅ **Bước cuối của mỗi task** = cập nhật tài liệu (xem R4.1)

### Lỗi #3 — Tự ý sửa schema Supabase

**Sự cố (tiềm ẩn):**
- AI có thể bị cám dỗ tự sửa schema để "fix lỗi"
- Vi phạm R3.1, nguy hiểm

**Cách tránh:**
- ✅ **LUÔN hỏi user** trước khi tạo migration mới
- ✅ Migration file generate ra → **KHÔNG apply** → đợi user

---

## Phụ lục — Quy trình khi nhận request mới

```
1. ĐỌC RULE.md và README.md trước                          ← hiểu context
2. Xác định role(s) chịu trách nhiệm                        ← xem mục 5
3. Lên kế hoạch + hỏi user nếu có điểm chưa rõ             ← xem R1.1
4. Code + Test                                              ← xem R4.2
5. Cập nhật tài liệu (README, RULE, doc/)                   ← xem R4.1
6. Báo cáo lại user                                         ← xem R4.3
7. KHÔNG tự ý commit/push trừ khi user yêu cầu             ← an toàn
```

---

## Lịch sử thay đổi

| Ngày | Thay đổi | Người |
|---|---|---|
| 2026-09-09 | Khởi tạo RULE.md sau sự cố đặt số 1.2.3.4 | AI Agent + User feedback |
