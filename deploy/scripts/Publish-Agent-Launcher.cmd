@echo off
setlocal
set "SCRIPT_DIR=%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Publish-App.ps1" ^
  -ProjectRelativePath "src\RemoteDesktop.Agent\RemoteDesktop.Agent.csproj" ^
  -OutputRelativePath "deploy\publish\Agent" ^
  -ExecutableName "RemoteDesktop.Agent.exe"

if errorlevel 1 exit /b %errorlevel%

start "" "%SCRIPT_DIR%..\publish\Agent\RemoteDesktop.Agent.exe"
