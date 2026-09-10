# 🤝 Hướng dẫn đóng góp — SWR AI Grading Server 1

> Repo này dùng **Git Flow** + **Conventional Commits**. Bạn làm theo hướng dẫn dưới đây.

---

## 🌿 Branch Strategy (Git Flow)

```
main (production)          ← code chạy được, deploy bất kỳ lúc nào
  └── develop              ← nhánh tích hợp, base cho mọi feature mới
        ├── feature/*      ← tính năng mới
        ├── bugfix/*       ← fix bug không urgent
        └── chore/*        ← viết doc, refactor nhỏ, update deps

hotfix/*                   ← fix bug khẩn cấp trên main (được phép từ main)
release/*                  ← chuẩn bị release (optional, dự án nhỏ có thể bỏ qua)
```

### Quy tắc

| Branch | Được phép push trực tiếp? | Phải qua PR? | Merge vào |
|---|---|---|---|
| `main` | ❌ | ✅ | — |
| `develop` | ❌ | ✅ | — |
| `feature/*` | ✅ | ✅ PR vào `develop` | `develop` |
| `bugfix/*` | ✅ | ✅ PR vào `develop` | `develop` |
| `chore/*` | ✅ | ✅ PR vào `develop` | `develop` |
| `hotfix/*` | ✅ | ✅ PR vào `main` + `develop` | `main` + `develop` |

### Tên branch

```powershell
# ✅ Tốt
feature/add-entity-exam
feature/migration-initial
bugfix/fix-jwt-expiry
chore/update-readme
hotfix/fix-login-crash

# ❌ Xấu
my-branch
test
fix
new-thing
```

---

## 💬 Commit Convention (Conventional Commits)

### Format

```
<type>(<scope>): <subject>

<body — tuỳ chọn>

<footer — tuỳ chọn>
```

### Type

| Type | Khi nào dùng | Ví dụ |
|---|---|---|
| `feat` | Tính năng mới | `feat(entities): add Exam entity` |
| `fix` | Sửa bug | `fix(jwt): fix expiry check` |
| `docs` | Chỉ doc | `docs(readme): update setup guide` |
| `style` | Format code, không đổi logic | `style(api): format C# files` |
| `refactor` | Sửa code, không đổi behavior | `refactor(domain): extract base entity` |
| `test` | Thêm/sửa test | `test(service): add unit test for ExamService` |
| `chore` | Viết doc, update deps, build | `chore(deps): bump EF Core to 8.0.0` |
| `perf` | Tối ưu performance | `perf(db): add index on Exam.CourseId` |
| `ci` | CI/CD | `ci(github): add build workflow` |

### Scope (optional, viết trong ngoặc)

```
feat(api/entities): ...
fix(api/middleware): ...
docs(doc/01-assignment): ...
chore(scripts): ...
```

### Subject

- **KHÔNG** viết hoa chữ cái đầu
- **KHÔNG** kết thúc bằng dấu chấm
- **TỐI ĐA 72 ký tự**
- Dùng động từ ở thì hiện tại: `add`, `fix`, `update`, `remove`, `refactor`

### Ví dụ đầy đủ

```
feat(entities): add Exam, Question, Choice entities

- Exam: Id, Title, CreatedBy, CourseId, StartTime, EndTime
- Question: Id, Content, Type, Points, ExamId
- Choice: Id, Content, IsCorrect, QuestionId
- All inherit BaseEntity (Id, CreatedAt, UpdatedAt, IsDeleted)

Refs: #12
```

---

## 🔄 Workflow điển hình

### Bắt đầu 1 feature mới

```powershell
# 1. Cập nhật develop
git checkout develop
git pull origin develop

# 2. Tạo feature branch
git checkout -b feature/your-feature-name

# 3. Code, commit nhỏ + rõ ràng
git add .
git commit -m "feat(scope): short description"

# 4. Push feature branch
git push -u origin feature/your-feature-name

# 5. Tạo Pull Request vào develop trên GitHub
# 6. Review (tự review hoặc nhờ teammate)
# 7. Merge vào develop → xoá feature branch
```

### Khi develop đã stable, merge vào main

```powershell
# 1. Tạo PR từ develop → main
# 2. Review kỹ
# 3. Merge → tag version
git checkout main
git pull origin main
git merge --no-ff develop
git tag -a v1.0.0 -m "Release 1.0.0"
git push origin main --tags
```

### Hotfix (fix bug khẩn cấp trên main)

```powershell
# 1. Tạo hotfix từ main
git checkout main
git checkout -b hotfix/critical-bug

# 2. Fix + commit
git commit -m "fix(api): critical bug description"

# 3. Merge vào main
git checkout main
git merge --no-ff hotfix/critical-bug
git push origin main

# 4. Merge ngược vào develop
git checkout develop
git merge --no-ff hotfix/critical-bug
git push origin develop

# 5. Xoá hotfix branch
git branch -d hotfix/critical-bug
```

---

## ✅ Pull Request Checklist

Trước khi tạo PR, đảm bảo:

```
□ Code đã build thành công (`dotnet build`)
□ Test pass (nếu có)
□ Không có file .env, appsettings.Development.json trong diff
□ README hoặc doc liên quan đã update (nếu cần)
□ Commit messages theo Conventional Commits
□ Branch đã rebase với develop mới nhất
```

---

## 🛡️ Bảo mật

- **KHÔNG** commit file `.env`, `appsettings.Development.json`, bất kỳ file nào có secret
- Xem chi tiết: [SECRET_MANAGEMENT.md](./SECRET_MANAGEMENT.md)
- Nếu lỡ commit secret → báo ngay cho team lead

---

## 📞 Liên hệ

- **Tác giả:** TheTLV
- **Môn học:** SWR30x (Software Requirements)
- **Mã SV:** FA26SE034

---

📌 **Xem thêm:**
- [README.md](./README.md) — tổng quan dự án
- [doc/01-assignment/](./doc/01-assignment/) — yêu cầu đồ án
- [SECRET_MANAGEMENT.md](./SECRET_MANAGEMENT.md) — quản lý secret
