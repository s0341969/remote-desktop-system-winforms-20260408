@echo off
setlocal
set SCRIPT_DIR=%~dp0
set SERVER_DIR=%SCRIPT_DIR%..\publish\Server
set EXE_PATH=%SERVER_DIR%\RemoteDesktop.Server.exe

if not exist "%EXE_PATH%" (
    echo Server publish executable not found: "%EXE_PATH%"
    exit /b 1
)

pushd "%SERVER_DIR%"
start "RemoteDesktop Server" "%EXE_PATH%"
popd
endlocal
