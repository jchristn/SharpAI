@echo off
setlocal enabledelayedexpansion

REM ==========================================================================
REM reset.bat - Reset SharpAI docker environment to factory defaults
REM
REM This script stops SharpAI containers, destroys runtime state, and restores
REM factory-default docker/sharpai.json and docker/sharpai.db.
REM
REM Usage: factory\reset.bat
REM ==========================================================================

set "SCRIPT_DIR=%~dp0"
set "DOCKER_DIR=%SCRIPT_DIR%..\"
set "FACTORY_DIR=%SCRIPT_DIR%"

if not exist "%FACTORY_DIR%sharpai.json" (
    echo ERROR: Factory config not found: %FACTORY_DIR%sharpai.json
    exit /b 1
)

if not exist "%FACTORY_DIR%sharpai.db" (
    echo ERROR: Factory database not found: %FACTORY_DIR%sharpai.db
    exit /b 1
)

echo.
echo ==========================================================
echo   SharpAI - Reset Docker Environment to Factory Defaults
echo ==========================================================
echo.
echo WARNING: This is a DESTRUCTIVE action. The following will
echo be permanently reset or deleted:
echo.
echo   - docker\sharpai.json restored from factory defaults
echo   - docker\sharpai.db restored from factory defaults
echo   - SQLite WAL/SHM sidecar files
echo   - All Docker log files
echo   - All Docker temp files
echo   - All downloaded GGUF models (sharpai-models volume)
echo.
set /p "CONFIRM=Type 'RESET' to confirm: "
echo.

if not "%CONFIRM%"=="RESET" (
    echo Aborted. No changes were made.
    exit /b 1
)

echo [1/5] Stopping containers...
pushd "%DOCKER_DIR%" >nul
docker compose down 2>nul
docker compose -f compose-cpu.yaml down 2>nul
docker compose -f compose-cuda.yaml down 2>nul
popd >nul

echo [2/5] Restoring factory configuration and database...
del /q "%DOCKER_DIR%sharpai.json" 2>nul
copy /y "%FACTORY_DIR%sharpai.json" "%DOCKER_DIR%sharpai.json" >nul

del /q "%DOCKER_DIR%sharpai.db" 2>nul
del /q "%DOCKER_DIR%sharpai.db-shm" 2>nul
del /q "%DOCKER_DIR%sharpai.db-wal" 2>nul
copy /y "%FACTORY_DIR%sharpai.db" "%DOCKER_DIR%sharpai.db" >nul
echo         Restored sharpai.json and sharpai.db

echo [3/5] Resetting runtime directories...
if not exist "%DOCKER_DIR%logs" mkdir "%DOCKER_DIR%logs" 2>nul
if not exist "%DOCKER_DIR%temp" mkdir "%DOCKER_DIR%temp" 2>nul

del /q "%DOCKER_DIR%logs\*" 2>nul
del /q "%DOCKER_DIR%temp\*" 2>nul
for /d %%d in ("%DOCKER_DIR%logs\*") do rd /s /q "%%d" 2>nul
for /d %%d in ("%DOCKER_DIR%temp\*") do rd /s /q "%%d" 2>nul
echo         Cleared logs and temp files

echo [4/5] Removing downloaded models...
docker volume rm sharpai-models 2>nul
echo         Removed sharpai-models volume

echo [5/5] Factory reset complete.
echo.
echo To start CPU mode:
echo   cd %DOCKER_DIR%
echo   docker compose -f compose-cpu.yaml up -d
echo.
echo To start CUDA mode:
echo   cd %DOCKER_DIR%
echo   docker compose -f compose-cuda.yaml up -d
echo.

endlocal
