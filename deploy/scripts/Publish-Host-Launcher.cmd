@echo off
setlocal
set "SCRIPT_DIR=%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Publish-App.ps1" ^
  -ProjectRelativePath "src\RemoteDesktop.Host\RemoteDesktop.Host.csproj" ^
  -OutputRelativePath "deploy\publish\Host" ^
  -ExecutableName "RemoteDesktop.Host.exe"

if errorlevel 1 exit /b %errorlevel%

start "" "%SCRIPT_DIR%..\publish\Host\RemoteDesktop.Host.exe"
