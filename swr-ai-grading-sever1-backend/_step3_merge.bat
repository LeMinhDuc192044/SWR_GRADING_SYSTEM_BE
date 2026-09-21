@echo off
cd /d D:\FPT\2025_k9\DoAn\SWR_GRADING_SYSTEM_BE\swr-ai-grading-sever1-backend
echo ===MERGE===
git merge origin/main --no-ff -m "Merge origin/main into DB_Update (sync DB schema)"
echo ===EXIT_CODE===%ERRORLEVEL%
echo ===STATUS===
git status
