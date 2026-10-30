@echo off
setlocal

rem Rebuild the current source and replace an existing ECS deployment.
where pwsh >nul 2>&1
if errorlevel 1 goto windows_powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0SpideyNet\deploy\ecs\Manage-Server.ps1" -Action Update %*
goto finished

:windows_powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0SpideyNet\deploy\ecs\Manage-Server.ps1" -Action Update %*

:finished
set "SERVER_EXIT_CODE=%errorlevel%"
if not "%SERVER_EXIT_CODE%"=="0" echo Server update failed. See the error above. 1>&2
if not defined SPIDEYNET_NO_PAUSE pause
exit /b %SERVER_EXIT_CODE%
