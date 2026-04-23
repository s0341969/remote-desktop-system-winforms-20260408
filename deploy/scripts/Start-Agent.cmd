@echo off
setlocal
if /i "%PROCESSOR_ARCHITECTURE%"=="x86" if not defined PROCESSOR_ARCHITEW6432 (
  set "AGENT_EXE=%~dp0..\publish\Agent\RemoteDesktop.Agent.x86.exe"
) else (
  set "AGENT_EXE=%~dp0..\publish\Agent\RemoteDesktop.Agent.exe"
)

if not exist "%AGENT_EXE%" (
  echo Agent publish output not found. Rebuild deploy\publish first.
  exit /b 1
)

start "" "%AGENT_EXE%"
