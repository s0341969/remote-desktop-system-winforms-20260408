@echo off
setlocal
cd /d "%~dp0..\publish\Agent"
start "" "%~dp0..\publish\Agent\RemoteDesktop.Agent.exe"
