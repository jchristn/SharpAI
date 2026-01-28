@echo off

IF "%1" == "" GOTO :Usage

if not exist sharpai.json (
  echo Configuration file sharpai.json not found.
  exit /b 1
)

REM Items that require persistence
REM   sharpai.json
REM   sharpai.db
REM   logs/
REM   models/

REM Argument order matters!

docker run ^
  -p 8000:8000 ^
  -t ^
  -i ^
  -e "TERM=xterm-256color" ^
  -v .\sharpai.json:/app/sharpai.json ^
  -v .\sharpai.db:/app/sharpai.db ^
  -v .\logs\:/app/logs/ ^
  -v .\models\:/app/models/ ^
  jchristn77/sharpai:%1

GOTO :Done

:Usage
ECHO Provide one argument indicating the tag. 
ECHO Example: dockerrun.bat v1.0.0
:Done
@echo on
