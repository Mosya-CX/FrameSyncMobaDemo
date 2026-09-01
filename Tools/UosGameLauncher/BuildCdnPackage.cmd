@echo off
setlocal
if "%~1"=="" (
  echo Usage: BuildCdnPackage.cmd ^<version^>
  echo Example: BuildCdnPackage.cmd 1.0.0
  exit /b 2
)
pushd "%~dp0"
dotnet run --project "FrameSyncMoba.GameLauncher.csproj" -c Release -- --build-cdn-package --version "%~1"
if errorlevel 1 (
  echo.
  echo CDN package build failed.
  popd
  exit /b 1
)
echo.
echo Upload directory: Builds\CdnUpload\%~1\Upload
popd
endlocal
