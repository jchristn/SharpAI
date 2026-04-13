@echo off
REM
REM Docker build script for SharpAI Server (Windows)
REM Builds a Docker image with both CPU and GPU backend support
REM

setlocal

set VERSION=%1
if "%VERSION%"=="" set VERSION=latest

set IMAGE_NAME=sharpai
set FULL_IMAGE_NAME=%IMAGE_NAME%:%VERSION%

echo ==================================
echo Building SharpAI Docker Image
echo ==================================
echo Image: %FULL_IMAGE_NAME%
echo Build directory: %~dp0..\src
echo.

REM Navigate to src directory
cd /d "%~dp0..\src"

REM Build the Docker image
echo Starting Docker build...
docker build -f SharpAI.Server/Dockerfile -t %FULL_IMAGE_NAME% .

if errorlevel 1 (
    echo.
    echo Build failed!
    exit /b 1
)

echo.
echo ==================================
echo Build Complete!
echo ==================================
echo Image: %FULL_IMAGE_NAME%
echo.
echo To run in CPU mode:
echo   docker run -p 8000:8000 %FULL_IMAGE_NAME%
echo.
echo To run in GPU mode (requires NVIDIA Docker):
echo   docker run --gpus all -p 8000:8000 %FULL_IMAGE_NAME%
echo.

endlocal
