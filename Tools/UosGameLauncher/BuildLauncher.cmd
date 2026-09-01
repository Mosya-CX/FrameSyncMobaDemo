@echo off
setlocal
pushd "%~dp0"
if not exist "CdnSigningPublicKey.pem" (
  echo.
  echo Missing CdnSigningPublicKey.pem. Run GenerateCdnSigningKey.cmd once before publishing.
  popd
  exit /b 1
)
dotnet publish "FrameSyncMoba.GameLauncher.csproj" -c Release -r win-x64 --self-contained true -o "..\..\Builds\Demo\Launcher"
if errorlevel 1 (
  echo.
  echo FrameSync MOBA launcher publish failed.
  popd
  exit /b 1
)
echo.
echo Launcher published to Builds\Demo\Launcher
popd
endlocal
