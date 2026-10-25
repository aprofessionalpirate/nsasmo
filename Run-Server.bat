@echo off
setlocal

rem Resolve the deployment from this file, including when started by double-click.
where pwsh >nul 2>&1
if errorlevel 1 goto windows_powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0SpideyNet\deploy\ecs\Manage-Server.ps1" -Action Run %*
goto finished

:windows_powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0SpideyNet\deploy\ecs\Manage-Server.ps1" -Action Run %*

:finished
set "SERVER_EXIT_CODE=%errorlevel%"
if not "%SERVER_EXIT_CODE%"=="0" echo Server startup failed. See the error above. 1>&2
if not defined SPIDEYNET_NO_PAUSE pause
exit /b %SERVER_EXIT_CODE%
