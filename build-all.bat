@ECHO OFF
SETLOCAL
IF "%~1" == "" GOTO :Usage

PUSHD "%~dp0"
IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%

CALL build-server.bat %*
IF ERRORLEVEL 1 GOTO :Failed
CALL build-dashboard.bat %*
IF ERRORLEVEL 1 GOTO :Failed
CALL build-tui.bat
IF ERRORLEVEL 1 GOTO :Failed

POPD
EXIT /B 0

:Failed
SET "BUILD_EXIT_CODE=%ERRORLEVEL%"
POPD
EXIT /B %BUILD_EXIT_CODE%

:Usage
ECHO Provide a tag argument for the build.
ECHO Example: build-all.bat v4.0.1
EXIT /B 1
