@echo off
setlocal
set SCRIPT_DIR=%~dp0

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Publish-App.ps1" ^
  -ProjectRelativePath "src\RemoteDesktop.Server\RemoteDesktop.Server.csproj" ^
  -OutputRelativePath "deploy\publish\Server" ^
  -ExecutableName "RemoteDesktop.Server.exe" ^
  -Framework "net8.0"

endlocal
