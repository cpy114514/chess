@echo off
setlocal EnableExtensions EnableDelayedExpansion

cd /d "%~dp0"
for /f "delims=" %%B in ('git rev-parse --abbrev-ref HEAD') do set "BRANCH=%%B"
if not defined BRANCH (
  echo Failed to detect current git branch.
  exit /b 1
)

echo [1/4] Staging changes...
git add -A
if errorlevel 1 goto :error

git diff --cached --quiet
if not errorlevel 1 (
  echo No changes to commit.
) else (
  echo [2/4] Committing changes...
  for /f "delims=" %%T in ('powershell -NoProfile -Command "Get-Date -Format ''yyyy-MM-dd HH:mm:ss''"') do set "STAMP=%%T"
  if not defined STAMP set "STAMP=now"
  git commit -m "Update project !STAMP!"
  if errorlevel 1 goto :error
)

echo [3/4] Pushing to origin/!BRANCH!...
git push origin !BRANCH!
if errorlevel 1 goto :error

echo [4/4] Done.
exit /b 0

:error
echo Upload failed.
exit /b 1
