@echo off
setlocal
cd /d "%~dp0..\publish\Host"
start "" "%~dp0..\publish\Host\RemoteDesktop.Host.exe"
