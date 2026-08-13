@echo off
setlocal
cd /d "%~dp0"
dotnet publish FrameSyncMoba.UosClientLauncher.csproj -c Release -r win-x64 --self-contained false -o "..\..\Builds\Tools\UosClientLauncher"
if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)
echo.
echo Launcher built at:
echo %CD%\..\..\Builds\Tools\UosClientLauncher\FrameSyncMoba.UosClientLauncher.exe
pause
