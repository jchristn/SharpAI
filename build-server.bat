@ECHO OFF
IF "%1" == "" GOTO :Usage
ECHO.
ECHO Building SharpAI server for linux/amd64 and linux/arm64/v8...
docker buildx build -f src/SharpAI.Server/Dockerfile --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag jchristn77/sharpai:%1 --tag jchristn77/sharpai:latest --push src

GOTO :Done

:Usage
ECHO Provide a tag argument for the build.
ECHO Example: build-server.bat v4.0.0

:Done
ECHO Done
@ECHO ON
