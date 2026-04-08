@echo off
setlocal
set "HOST_EXE=%~dp0..\publish\Host\RemoteDesktop.Host.exe"

if not exist "%HOST_EXE%" (
  echo Host publish output not found. Run deploy\scripts\Publish-Host-Launcher.cmd first.
  exit /b 1
)

start "" "%HOST_EXE%"
