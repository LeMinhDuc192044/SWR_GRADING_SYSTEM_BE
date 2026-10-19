# 📚 API Statistics - Backend Server 1 (SWR302 Grading System)

> **Mục đích:** Note cá nhân để đọc và thực hành khi làm việc với Backend Server 1.
>
> **Cập nhật lần cuối:** 2026-09-20
>
> **Server:** `http://localhost:5290` | **Swagger:** `http://localhost:5290/swagger`

---

## 🎯 Mục lục

1. [Tổng quan](#-tổng-quan)
2. [Auth API (`/api/auth`)](#1-auth-api-apiauth)
3. [Health API (`/api/health`)](#2-health-api-apihealth)
4. [Semesters API (`/api/semesters`)](#3-semesters-api-apisemesters)
5. [PaperSets API (`/api/paper-sets`)](#4-papersets-api-apipaper-sets)
6. [Examinations API (`/api/examinations`)](#5-examinations-api-apiexaminations)
7. [GradingDiaries API (`/api/grading-diaries`)](#6-gradingdiaries-api-apigrading-diaries)
8. [Submissions API (`/api/submissions`)](#7-submissions-api-apisubmissions)
9. [Complete Flow (Flow 1 - End-to-End)](#-complete-flow-flow-1---end-to-end)
10. [Test Accounts](#-test-accounts-development)

---

## 📊 Tổng quan

| # | Module | Endpoints | Auth Required |
|---|--------|-----------|---------------|
| 1 | Auth | 2 | ❌ |
| 2 | Health | 1 | ❌ |
| 3 | Semesters | 5 | ❌ |
| 4 | PaperSets | 9 | 🔒 Một số |
| 5 | Examinations | 4 | ❌ |
| 6 | GradingDiaries | 4 | ✅ Có |
| 7 | Submissions | 6 (+routes duplicate) | ✅ Có |
| **Tổng** | **7 controllers** | **31 endpoints** | |

**Pattern chung:** Tất cả response đều wrap trong `ApiResponse<T>`:

```json
{
  "code": 200,
  "data": { ... }
}
```

Khi lỗi:

```json
{
  "code": 400 | 401 | 403 | 404 | 409,
  "error": "Error message"
}
```

---

## 1. Auth API (`/api/auth`)

> **Lý do có:** Mỗi request cần authentication, sinh viên/giảng viên/admin phải login trước.

### `POST /api/auth/login`

**Tác dụng:** Đăng nhập, trả về JWT access token.

**Khi nào dùng:**
- Trước khi gọi API nào cần auth
- Trong script test (lưu token → dùng cho các request sau)

**Request:**
```json
{
  "email": "giang@test.com",
  "password": "Giang@123"
}
```

**Response (200):**
```json
{
  "code": 200,
  "data": {
    "accessToken": "eyJhbGciOi...",
    "tokenType": "Bearer",
    "expiresInMinutes": 60,
    "user": {
      "id": "e2ba9077-efda-4c07-8ffb-2ad70094e82b",
      "fullName": "Nguyen Van Giang",
      "role": "Lecturer"
    }
  }
}
```

**Validation:**
- Email: bắt buộc, lowercase trim
- Password: bắt buộc
- Account phải `IsActive=true` và `IsDeleted=false`
- Password verify bằng BCrypt

**Errors:** `401 Invalid credentials`

### `POST /api/auth/register`

**Tác dụng:** Tạo tài khoản mới (Student/Lecturer/Admin).

**Khi nào dùng:**
- Lúc đầu setup dữ liệu
- Admin thêm giảng viên/sinh viên

**Request:**
```json
{
  "fullName": "Nguyen Van A",
  "email": "a@test.com",
  "password": "Password@123",
  "cccd": "001234567890",
  "birthday": "2000-01-01",
  "role": 0,
  "studentCode": "SE170001",
  "major": "Software Engineering"
}
```

**Logic:** Role = 0 (Student) → cần `studentCode` + `major`. Role = 1 (Lecturer) → cần `lecturerCode` + `subject`. Role = 2 (Admin) → chỉ cần CCCD.

**Errors:** `400` validation, `409` email/code tồn tại.

---

## 2. Health API (`/api/health`)

### `GET /api/health`

**Tác dụng:** Check server còn sống không. Trả về `status: healthy`.

**Khi nào dùng:**
- DevOps monitoring, load balancer
- Test nhanh server đã start chưa

**Response:**
```json
{
  "code": 200,
  "data": {
    "status": "healthy",
    "server": "Backend.Server1",
    "time": "2026-09-20T..."
  }
}
```

---

## 3. Semesters API (`/api/semesters`)

> **Lý do có:** Học kỳ là đơn vị gốc để nhóm kỳ thi (FA26, SP27...).

### `GET /api/semesters?Page=1&PageSize=10`

**Tác dụng:** Lấy danh sách học kỳ (phân trang).

**Khi nào dùng:**
- Load dropdown chọn học kỳ trong UI tạo Paper Set
- Hiển thị danh sách cho admin

### `GET /api/semesters/{id}`

**Tác dụng:** Lấy chi tiết 1 học kỳ + danh sách Examination + PaperSet liên quan.

**Khi nào dùng:** Xem chi tiết 1 học kỳ cụ thể.

### `POST /api/semesters`

**Tác dụng:** Tạo học kỳ mới.

**Request:**
```json
{
  "semesterCode": "FA26",
  "name": "Fall 2026",
  "startDate": "2026-09-01",
  "endDate": "2026-12-31",
  "status": 0
}
```

**Errors:** `409` semesterCode trùng.

### `PUT /api/semesters/{id}`

**Tác dụng:** Cập nhật thông tin học kỳ.

### `DELETE /api/semesters/{id}`

**Tác dụng:** Xóa học kỳ (mềm).

**Lưu ý:** Không xóa được nếu còn Examination liên kết.

### `PATCH /api/semesters/{id}/status`

**Tác dụng:** Đổi status học kỳ (Active/Inactive).

---

## 4. PaperSets API (`/api/paper-sets`)

> **Lý do có:** Paper Set = đề thi gốc. Mỗi đề có file câu hỏi (.docx) + rubric (.xlsx) + template (.docx).

### `GET /api/paper-sets?Page=1&PageSize=10`

**Tác dụng:** Danh sách Paper Sets (phân trang).

### `POST /api/paper-sets/preview-questions`

**Tác dụng:** Upload file .docx câu hỏi → server parse ra danh sách câu hỏi tự động (dùng cho UI preview trước khi submit).

**Khi nào dùng:** Khi muốn xem trước cấu trúc câu hỏi từ file Word.

**Request:** `multipart/form-data` với `question` field.

### `GET /api/paper-sets/{id}`

**Tác dụng:** Lấy metadata + danh sách câu hỏi + URLs file.

**Khi nào dùng:** Hiển thị chi tiết 1 paper set.

### `GET /api/paper-sets/{id}/content?fileType=AR`

**Tác dụng:** Download file rubric/câu hỏi từ Supabase Storage.

### `POST /api/paper-sets` ⭐

**Tác dụng:** Tạo paper set MỚI với file + câu hỏi + liên kết học kỳ.

**Khi nào dùng:** Bước đầu tiên khi giảng viên muốn upload đề thi mới.

**Request:** `multipart/form-data`
- `SemesterId` (Guid, bắt buộc)
- `Description` (string)
- `Question` (file .docx/.doc - bắt buộc)
- `AnswerRubric` (file .xlsx/.xls - bắt buộc)
- `AnswerTemplate` (file .docx - optional)
- `Questions[0].Title`, `Questions[0].Point`, ...

**Auth:** Required (Lecturer/Admin)

**Response:**
```json
{
  "code": 200,
  "data": [
    {
      "paperSetId": "c4da3f9c-faef-4140-b602-959ade5c482b",
      "paperSetCode": "EM613158",
      "totalQuestions": 2,
      "files": [
        { "fileName": "Exam_Questions.docx", "fileType": "EQ" },
        { "fileName": "sample_rubric.xlsx", "fileType": "AR" }
      ]
    }
  ]
}
```

**Validation:**
- File extension theo FileType: Question/AnswerTemplate = `.doc/.docx`, AnswerRubric = `.xls/.xlsx`
- Lecturers role mới được tạo

### `POST /api/paper-sets/batch`

**Tác dụng:** Tạo NHIỀU paper set cùng lúc.

**Khi nào dùng:** Khi admin upload nhiều đề cùng lúc.

### `POST /api/paper-sets/{id}/files`

**Tác dụng:** Thêm file (rubric/template/câu hỏi) vào paper set đã có.

### `PUT /api/paper-sets/{id}`

**Tác dụng:** Update metadata (description, totalQuestions, status...).

### `DELETE /api/paper-sets/{id}`

**Tác dụng:** Xóa mềm paper set.

---

## 5. Examinations API (`/api/examinations`)

> **Lý do có:** Examination = Kỳ thi (PE/RE/3W). Liên kết PaperSet với học kỳ → tạo context cho chấm thi.

### `GET /api/examinations?Page=1&PageSize=10`

**Tác dụng:** Danh sách kỳ thi.

### `GET /api/examinations/{id}`

**Tác dụng:** Chi tiết 1 kỳ thi.

### `POST /api/examinations` ⭐

**Tác dụng:** Tạo kỳ thi mới + tự động link PaperSet vào Examination.

**Request:**
```json
{
  "name": "Ky thi cuoi ky SWR302",
  "examinationType": 1,
  "startDate": "2026-09-20",
  "startTime": "08:00:00",
  "durationMinutes": 90,
  "beforeTimeMinutes": 15,
  "note": "Thi tren may tinh co giam sat",
  "status": 0,
  "semesterId": "81fcad83-...",
  "paperSetId": "c4da3f9c-..."
}
```

**Lưu ý quan trọng:**
- `examinationType`: 0=RE, 1=PE, 2=ThreeW
- `semesterId` phải match với semester của `paperSetId` (validation)
- Examination phải tạo TRƯỚC khi upload submission (vì `PaperSet.ExaminationId` cần có giá trị)

**Validation:**
- Semester không tồn tại → `404`
- PaperSet không tồn tại → `404`
- PaperSet không thuộc semester → `404 PAPER_SET_SEMESTER_MISMATCH`

**Code generation:** `examinationCode` tự sinh: `{semesterCode}_{typeCode}_{random}` → ví dụ `SWR302_FA26_PE_885949`

### `PUT /api/examinations/{id}`

**Tác dụng:** Update thông tin kỳ thi.

### `DELETE /api/examinations/{id}`

**Tác dụng:** Xóa kỳ thi (mềm). Lỗi nếu còn PaperSet liên kết.

---

## 6. GradingDiaries API (`/api/grading-diaries`) 🔒

> **Lý do có:** Grading Diary = Sổ chấm thi. Mỗi sổ gắn với 1 PaperSet + 1 Lecturer. Chứa danh sách bài nộp của sinh viên.
>
> **Auth:** Required

### `POST /api/grading-diaries` ⭐

**Tác dụng:** Tạo sổ chấm thi từ 1 PaperSet đã có.

**Khi nào dùng:** Sau khi tạo Examination, tạo sổ chấm để bắt đầu chấm bài.

**Request:**
```json
{
  "paperSetId": "c4da3f9c-...",
  "name": "So cham thi - EM613158",
  "content": "EM613158"
}
```

**Lưu ý:**
- Mỗi PaperSet chỉ tạo được 1 Diary (rule `PAPER_SET_ALREADY_HAS_DIARY`)
- Chỉ Lecturer/Admin mới tạo được
- `content` thường = paperSetCode (dùng làm folder cho submissions storage)

**Errors:**
- `404 PAPER_SET_NOT_FOUND`
- `403 LECTURER_REQUIRED` (nếu role khác)
- `409 PAPER_SET_ALREADY_HAS_DIARY`

### `GET /api/grading-diaries?Page=1&PageSize=10` 🔒

**Tác dụng:** Lấy danh sách sổ chấm.

**Auth:** Lecturer thấy diary của mình. Admin thấy tất cả.

### `GET /api/grading-diaries/{id}` 🔒

**Tác dụng:** Chi tiết 1 sổ chấm + danh sách submissions.

### `PUT /api/grading-diaries/{id}` 🔒

**Tác dụng:** Update tên/mô tả sổ chấm.

### `DELETE /api/grading-diaries/{id}` 🔒

**Tác dụng:** Xóa sổ chấm.

**Lỗi:** `400 DIARY_HAS_SUBMISSIONS` nếu còn bài nộp.

---

## 7. Submissions API (`/api/submissions`) 🔒

> **Lý do có:** Submission = bài làm sinh viên. Đây là PHẦN LÕI của flow chấm thi AI.
>
> **Auth:** Required (chỉ Lecturer/Admin thấy được)

### Routes:

API này support 2 dạng route:
- `/api/submissions/{id}/...` 
- `/api/grading-diaries/{diaryId}/submissions/{id}/...` (Flow 1 style)

### `POST /api/grading-diaries/{diaryId}/submissions/upload` ⭐ Flow 1

**Tác dụng:** Upload danh sách file `.docx` bài làm của sinh viên vào sổ chấm.

**Khi nào dùng:** Sau khi tạo sổ chấm, giảng viên upload bài để AI chấm.

**Request:** `multipart/form-data`
- File `.docx` của từng sinh viên (name file = studentCode + extension)
- Server **tự động bóc mã SV** từ tên file → match với `student_examination`

**Lưu ý tiền điều kiện:**
- `PaperSet.ExaminationId` phải có (đã tạo Examination trước)
- File phải là `.docx`

**Errors:**
- `400` không có file
- `404 DIARY_NOT_FOUND`
- `403 FORBIDDEN`

### `GET /api/grading-diaries/{diaryId}/submissions` 🔒

**Tác dụng:** Danh sách bài nộp trong sổ chấm.

### `GET /api/submissions/{id}` 🔒

**Tác dụng:** Chi tiết 1 bài nộp (kèm AI score, lecturer score, criteriaScores).

### `POST /api/submissions/{id}/ai-grade` ⭐ Flow 2

**Tác dụng:** Kích hoạt AI chấm bài (Gemini 3.6 Flash) theo Rubric.

**Quy trình:**
1. Download file `.xlsx` rubric + file `.docx` bài làm từ Supabase Storage
2. Gửi cho Gemini API
3. Parse response JSON → lưu `aiScore`, `criteriaScores`, `aiLogs`
4. Update submission status: `Submitted` → `AI_Graded`

**Khi nào dùng:** Sau khi upload, muốn AI chấm tự động.

### `PUT /api/submissions/{id}/review` ⭐ Flow 3

**Tác dụng:** Giảng viên nhập điểm + nhận xét (override AI score).

**Request:**
```json
{
  "lecturerScore": 8.5,
  "comment": "Bai lam tot, phan tich ro rang..."
}
```

**Side effect:** Status chuyển `AI_Graded` → `Lecturer_Reviewed`

**Lưu ý:** `aiScore` giữ nguyên, chỉ thêm `lecturerScore` song song.

### `PUT /api/submissions/{id}/finalize` ⭐ Flow 4

**Tác dụng:** Chốt điểm cuối cùng. Status chuyển sang `Final`.

**Khi nào dùng:** Sau khi giảng viên review xong tất cả bài, muốn đóng sổ chấm.

**Lưu ý:** Phải ở status `Lecturer_Reviewed` mới finalize được.

---

## 🎯 Complete Flow (Flow 1 - End-to-End)

```
1. POST /api/auth/login             (lấy token)
       ↓
2. GET  /api/semesters              (lấy semesterId)
       ↓
3. POST /api/paper-sets             (tạo đề + upload rubric + câu hỏi)
       ↓ returns paperSetId, paperSetCode
       ↓
4. POST /api/examinations           (tạo kỳ thi, link PaperSet)
       ↓ returns examinationId
       ↓   (auto: PaperSet.ExaminationId = examinationId)
       ↓
5. POST /api/grading-diaries        (tạo sổ chấm từ paperSetId)
       ↓ returns diaryId
       ↓
6. POST /api/grading-diaries/{diaryId}/submissions/upload
       ↓   (upload nhiều file .docx bài sinh viên)
       ↓ returns submissionId
       ↓
7. POST /api/submissions/{submissionId}/ai-grade
       ↓   (Gemini chấm điểm tự động)
       ↓ returns aiScore, criteriaScores, status=AI_Graded
       ↓
8. PUT  /api/submissions/{submissionId}/review
       ↓   (giảng viên review + override điểm)
       ↓ status=Lecturer_Reviewed
       ↓
9. PUT  /api/submissions/{submissionId}/finalize
       ↓   (chốt điểm cuối)
       ↓ status=Final
```

---

## 🔐 Test Accounts (Development)

> Trong `appsettings.Development.json` (chưa seed vào DB - cần Service tạo user thật).

| Role | Email | Password | Code |
|------|-------|----------|------|
| Admin (2) | admin@test.com | Admin@123 | - |
| Lecturer (1) | giang@test.com | Giang@123 | GV001 |
| Student (0) | nam@test.com | Nam@123 | SE170001 |

**Lưu ý:** Test accounts ở trên CHƯA được seed vào database. Hiện tại muốn login phải tạo user thật qua `POST /api/auth/register` hoặc insert trực tiếp vào DB.

---

## 📝 Status Enums (quick ref)

### SubmissionStatus
- `0 = Submitted` - mới upload
- `1 = AI_Graded` - AI đã chấm
- `2 = Lecturer_Reviewed` - GV đã review
- `3 = Final` - chốt điểm

### UserRole
- `0 = Student`
- `1 = Lecturer`
- `2 = Admin`

### ExaminationType
- `0 = RE` (Retake Exam)
- `1 = PE` (Practical Exam)
- `2 = ThreeW` (3 Weeks)

### PaperSetFileType
- `EQ = Question` (.doc/.docx)
- `AR = AnswerRubric` (.xls/.xlsx)
- `AT = AnswerTemplate` (.doc/.docx)

---

## 💡 Tips khi làm việc với API

1. **Luôn login trước:** Tất cả API trừ `auth/*`, `health`, `semesters/*`, `paper-sets/*` (GET), `examinations/*` đều cần Bearer token.

2. **Lưu token + ids khi test thủ công:** Dùng Swagger hoặc Postman để dễ copy.

3. **Upload file = `multipart/form-data`:** Nhớ `Content-Type: multipart/form-data` chứ không phải `application/json`.

4. **SubmissionId có 2 cách truyền:** Qua route `/api/submissions/{id}/...` hoặc qua query.

5. **File naming quan trọng:** Khi upload bài SV, tên file phải chứa studentCode (vd: `SE170001_Assignment.docx`).

6. **File size limit:** Tất cả endpoint upload có `RequestSizeLimit(524_288_000)` = 500MB.

---

**📖 Tài liệu liên quan:**
- `RULE.md` (luật trong workspace)
- `doc/04-architecture/architecture.md` (kiến trúc chi tiết)
- Swagger UI tại `http://localhost:5290/swagger`
