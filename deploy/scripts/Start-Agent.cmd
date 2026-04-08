@echo off
setlocal
set "AGENT_EXE=%~dp0..\publish\Agent\RemoteDesktop.Agent.exe"

if not exist "%AGENT_EXE%" (
  echo Agent publish output not found. Run deploy\scripts\Publish-Agent-Launcher.cmd first.
  exit /b 1
)

start "" "%AGENT_EXE%"
