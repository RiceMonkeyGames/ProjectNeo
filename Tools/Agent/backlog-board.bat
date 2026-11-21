@echo off
setlocal
pushd "%~dp0..\.."
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0backlog-board.ps1" %*
set "EXIT_CODE=%errorlevel%"
popd
if not "%EXIT_CODE%"=="0" (
    echo.
    echo Failed to generate backlog board ^(exit code %EXIT_CODE%^).
    pause
    exit /b %EXIT_CODE%
)
endlocal
