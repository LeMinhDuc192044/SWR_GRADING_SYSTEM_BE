@echo off
cd /d D:\FPT\2025_k9\DoAn\SWR_GRADING_SYSTEM_BE\swr-ai-grading-sever1-backend
git push origin DB_Update 2>&1
echo ===EXIT===%ERRORLEVEL%
git status
