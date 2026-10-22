@echo off
cd /d "%~dp0"
dotnet run --project src\SharpAI.Publisher -- --channel archive %*
exit /b %errorlevel%
