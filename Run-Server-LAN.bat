@echo off
setlocal

rem Resolve the helper from this file so double-click and other working folders work.
where pwsh >nul 2>&1
if errorlevel 1 goto windows_powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0SpideyNet\Host-Relay-LAN.ps1" %*
goto finished

:windows_powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0SpideyNet\Host-Relay-LAN.ps1" %*

:finished
set "SERVER_EXIT_CODE=%errorlevel%"
if not "%SERVER_EXIT_CODE%"=="0" echo LAN relay startup failed. See the error above. 1>&2
if not defined SPIDEYNET_NO_PAUSE pause
exit /b %SERVER_EXIT_CODE%
