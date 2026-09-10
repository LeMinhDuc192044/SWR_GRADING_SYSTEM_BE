# 🔐 Quản lý Secret & Biến môi trường

> **QUY TẮC VÀNG:** Secret **KHÔNG BAO GIỜ** được commit lên git.

## 📂 Cấu trúc file

```
Backend_Server1/
├── .env.example        ← ✅ Template, push lên git
├── .env                ← ❌ Secret thật, KHÔNG push (đã được .gitignore loại trừ)
├── .gitignore          ← ✅ Đã loại trừ .env + appsettings.Development.json
└── src/4.WebAPI/
    ├── appsettings.json              ← ✅ Base config, KHÔNG có secret
    ├── appsettings.Development.json  ← ❌ Có secret, KHÔNG push
    └── appsettings.Production.json   ← ⚠️ Chỉ cho CI/CD (override bằng env vars)
```

## ✅ Đã làm

| File | Trạng thái | Lý do |
|---|---|---|
| `.env.example` | ✅ push lên git | Chỉ có placeholder, không có giá trị thật |
| `.env` | ❌ KHÔNG push | Chứa password Supabase, JWT secret, API keys |
| `appsettings.json` | ✅ push lên git | Chỉ chứa cấu hình chung, không có secret |
| `appsettings.Development.json` | ❌ KHÔNG push | Chứa connection string thật cho dev local |
| `appsettings.Production.json` | ⚠️ Tuỳ | Production nên dùng env vars từ CI/CD |

## 🔍 Cách kiểm tra secret có bị lộ không

Sau khi push, kiểm tra bằng:

```powershell
# Tìm pattern password trong tất cả file đã commit
git log -p | Select-String -Pattern "password|secret|key" -CaseSensitive:$false

# Hoặc dùng git-secrets (tool của AWS)
git secrets --scan
```

## 🚨 Nếu lỡ commit secret lên GitHub

**Phải xử lý NGAY — kể cả khi repo là private:**

```powershell
# 1. Xoá khỏi git history (DANGER — thay đổi toàn bộ lịch sử)
git filter-branch --force --index-filter `
  "git rm --cached --ignore-unmatch path/to/file-with-secret.json" `
  --prune-empty --tag-name-filter cat -- --all

# 2. Force push (cảnh báo team trước)
git push origin --force --all

# 3. ĐỔI NGAY tất cả secret đã lộ:
#    - Supabase: Project Settings → Database → Reset password
#    - JWT: Generate secret mới
#    - API keys: Rotate trên dashboard tương ứng
```

## 🔑 Cách tạo secret mạnh

### JWT Secret (≥ 32 ký tự, tốt nhất 64+)

```powershell
# PowerShell
-join ((65..90) + (97..122) + (48..57) + (33,35,36,37,38,42,43,45,61,63) | Get-Random -Count 64 | ForEach-Object {[char]$_})

# Hoặc dùng OpenSSL (nếu có Git Bash)
openssl rand -base64 64
```

### Connection string Supabase

Lấy từ: Supabase Dashboard → Project Settings → Database → Connection string → **URI**

```
postgresql://postgres:[YOUR-PASSWORD]@db.xxx.supabase.co:5432/postgres
```

## 🏗️ Production deployment — dùng gì?

| Nền tảng | Cách inject secret |
|---|---|
| **Azure App Service** | Application Settings → Environment variables |
| **AWS ECS/Fargate** | Secrets Manager + task definition |
| **Docker** | `--env-file .env` (KHÔNG bake vào image) |
| **Kubernetes** | `Secret` resource + `envFrom.secretKeyRef` |
| **GitHub Actions** | Repository Settings → Secrets and variables → Actions |
| **Local dev** | File `.env` (không commit) |

## 📋 Checklist trước khi push code

```
□ Không có file .env trong git status
□ Không có file appsettings.Development.json trong git status  
□ Không có password/secret/key trong code (.cs files)
□ .env.example đã cập nhật nếu có biến mới
```

## 🆘 Khi gặp sự cố

1. **Lỡ commit secret?** → Xem mục "Nếu lỡ commit secret" ở trên
2. **Không nhớ password Supabase?** → Reset password trên Supabase Dashboard
3. **JWT không work?** → Kiểm tra `JWT_SECRET` đủ ≥ 32 ký tự
4. **CORS bị block?** → Thêm origin vào `CORS_ALLOWED_ORIGINS`

---

📌 **Xem thêm:** [CONTRIBUTING.md](./CONTRIBUTING.md) để biết quy trình branch + commit.
