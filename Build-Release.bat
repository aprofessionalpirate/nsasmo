@echo off
setlocal
set "PROJECT_ROOT=%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 goto missing_sdk

rem Use paths relative to this script so it also works from another directory.
rem Isolate build artifacts from any executables already running in bin folders.
echo Publishing the Release client...
dotnet publish "%PROJECT_ROOT%SpideyNet\Client\Client.csproj" -c Release --artifacts-path "%PROJECT_ROOT%artifacts\release" -o "%PROJECT_ROOT%outputs" -m:1
if errorlevel 1 goto publish_failed

echo Publishing the Release relay...
dotnet publish "%PROJECT_ROOT%SpideyNet\Relay\Relay.csproj" -c Release --artifacts-path "%PROJECT_ROOT%artifacts\release" -o "%PROJECT_ROOT%outputs" -m:1
if errorlevel 1 goto publish_failed

echo.
echo Release executables are ready:
echo   "%PROJECT_ROOT%outputs\Client.exe"
echo   "%PROJECT_ROOT%outputs\Relay.exe"
pause
exit /b 0

:missing_sdk
echo Install the .NET 10 SDK and make sure dotnet is on PATH. 1>&2
pause
exit /b 1

:publish_failed
set "PUBLISH_EXIT_CODE=%errorlevel%"
echo Release publishing failed. See the error above. 1>&2
pause
exit /b %PUBLISH_EXIT_CODE%
