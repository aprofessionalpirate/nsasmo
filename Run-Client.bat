@echo off
setlocal
set "PROJECT_ROOT=%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 goto missing_sdk

rem Resolve paths from this script and share the existing Release publishing layout.
echo Publishing the Release client...
dotnet publish "%PROJECT_ROOT%SpideyNet\Client\Client.csproj" -c Release --artifacts-path "%PROJECT_ROOT%artifacts\release" -o "%PROJECT_ROOT%outputs" -m:1 --tl:off
if errorlevel 1 goto publish_failed

echo.
echo Starting "%PROJECT_ROOT%outputs\Client.exe"...
start "" /D "%PROJECT_ROOT%outputs" "%PROJECT_ROOT%outputs\Client.exe"
if errorlevel 1 goto launch_failed
set "CLIENT_EXIT_CODE=0"
goto finished

:missing_sdk
echo Install the .NET 10 SDK and make sure dotnet is on PATH. 1>&2
set "CLIENT_EXIT_CODE=1"
goto finished

:publish_failed
set "CLIENT_EXIT_CODE=%errorlevel%"
echo Client publishing failed. See the error above. 1>&2
echo If outputs\Client.exe is already running, close it before rebuilding. 1>&2
goto finished

:launch_failed
set "CLIENT_EXIT_CODE=%errorlevel%"
echo Client startup failed. See the error above. 1>&2

:finished
if not defined SPIDEYNET_NO_PAUSE pause
exit /b %CLIENT_EXIT_CODE%
