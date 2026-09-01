@echo off
setlocal
pushd "%~dp0"
call BuildLauncher.cmd
if errorlevel 1 (
  popd
  exit /b 1
)
dotnet run --project "FrameSyncMoba.GameLauncher.csproj" -c Release -- --build-bootstrap-package --version 1.3.1
if errorlevel 1 (
  echo.
  echo Bootstrap package build failed.
  popd
  exit /b 1
)
echo.
echo Bootstrap package is ready under Builds\Bootstrap.
popd
endlocal
