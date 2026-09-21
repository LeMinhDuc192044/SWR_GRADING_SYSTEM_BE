@echo off
cd /d D:\FPT\2025_k9\DoAn\SWR_GRADING_SYSTEM_BE\swr-ai-grading-sever1-backend
git add -A
git commit -m "Merge origin/main into DB_Update

Resolve 7 conflicts (uu tien main):
- Entities: GradingDiary, Lecturer, Submission (theo schema main 17/9)
- DbContext + IApplicationDbContext (Gradings alias StudentExamination cua main)
- Program.cs (giu PaperSetService, bo ExamMaterial)
- Migration snapshot (lay version main)

Xoa code plan B da loi thoi vi:
- Bảng gradings khong con (main da xoa entity)
- Bang exam_materials da doi ten thanh paper_sets
- Bang student_examination thay the Gradings
- Schema student_submission moi khong co cot folder

Con lai:
- Entity Submission (cua main)
- Entity GradingDiary, StudentExamination (cua main)
- Se viet lai flow sau khi dien jack task voi giang vien"

git log --oneline -3
echo ===STATUS===
git status
