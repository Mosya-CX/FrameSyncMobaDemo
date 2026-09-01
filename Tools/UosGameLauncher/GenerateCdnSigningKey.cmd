@echo off
setlocal
pushd "%~dp0"
dotnet run --project "FrameSyncMoba.GameLauncher.csproj" -c Release -- --generate-cdn-signing-key
if errorlevel 1 (
  echo.
  echo CDN signing key generation failed.
  popd
  exit /b 1
)
echo.
echo Back up Builds\CdnSigning\FrameSyncMobaCdnPrivateKey.pem securely.
echo Never upload or commit the private key.
popd
endlocal
