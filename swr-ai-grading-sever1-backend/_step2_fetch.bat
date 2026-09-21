@echo off
cd /d D:\FPT\2025_k9\DoAn\SWR_GRADING_SYSTEM_BE\swr-ai-grading-sever1-backend
echo ===FETCH===
git fetch origin
echo ===LOG_MAIN_HEAD===
git log --oneline origin/main -10
echo ===LOG_DB_UPDATE_HEAD===
git log --oneline DB_Update -5
echo ===COMMITS_IN_MAIN_NOT_IN_DB_UPDATE===
git log --oneline DB_Update..origin/main
echo ===COMMITS_IN_DB_UPDATE_NOT_IN_MAIN===
git log --oneline origin/main..DB_Update
