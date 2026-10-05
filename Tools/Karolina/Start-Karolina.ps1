param([string]$ProjectPath = '')
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
}
$karolinaProject = Join-Path $PSScriptRoot 'Karolina.Desktop/Karolina.Desktop.csproj'
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts'))
$karolinaOutput = Join-Path $artifactRoot 'current'
$desktopRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Karolina.Desktop'))
$activeApps = @(Get-CimInstance Win32_Process -Filter "name='Karolina.Desktop.exe'" | Where-Object {
    $_.ExecutablePath -and (([IO.Path]::GetFullPath($_.ExecutablePath)).StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or ([IO.Path]::GetFullPath($_.ExecutablePath)).StartsWith($desktopRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
})
if ($activeApps.Count -gt 0) { throw 'Karolina 正在运行。请从系统托盘选择“完全退出”后重新运行启动入口；窗口 X 只收起到托盘，更新仍使用同一个 current 目录。' }
& dotnet build $karolinaProject -c Release --output $karolinaOutput --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Karolina build failed' }
# 旧生成目录可恢复地移出程序目录；源码与 current 始终保留。
$retiredOutputRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Karolina/recovery/retired-build-outputs'))
New-Item -ItemType Directory -Path $retiredOutputRoot -Force | Out-Null
foreach ($oldOutput in @(Get-ChildItem -LiteralPath $artifactRoot -Directory | Where-Object Name -ne 'current')) {
    $resolvedOutput = [IO.Path]::GetFullPath($oldOutput.FullName)
    if (-not $resolvedOutput.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or ($oldOutput.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw '拒绝清理越界或链接的输出目录' }
    $retiredOutput = Join-Path $retiredOutputRoot $oldOutput.Name
    if (Test-Path -LiteralPath $retiredOutput) { throw '同名旧输出已有归档，请在文件管理器处理后再更新。' }
    Move-Item -LiteralPath $resolvedOutput -Destination $retiredOutput
}
$legacyBuildRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Karolina/builds'))
if (Test-Path -LiteralPath $legacyBuildRoot) {
    $activeLegacy = @(Get-CimInstance Win32_Process -Filter "name='Karolina.Desktop.exe'" | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($legacyBuildRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) })
    if ($activeLegacy.Count -eq 0) {
        foreach ($legacyOutput in @(Get-ChildItem -LiteralPath $legacyBuildRoot -Directory)) {
            $resolvedOutput = [IO.Path]::GetFullPath($legacyOutput.FullName)
            if ($legacyOutput.Name -match '^[a-f0-9]{32}$' -and $resolvedOutput.StartsWith($legacyBuildRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and -not ($legacyOutput.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                $retiredOutput = Join-Path $retiredOutputRoot $legacyOutput.Name
                if (Test-Path -LiteralPath $retiredOutput) { throw '同名旧输出已有归档，请在文件管理器处理后再更新。' }
                Move-Item -LiteralPath $resolvedOutput -Destination $retiredOutput
            }
        }
    }
}
# 这是用户要交互的桌面窗口；Hidden 会使 WinForms 的首次 Show 被 STARTUPINFO 隐藏。
Start-Process -FilePath (Join-Path $karolinaOutput 'Karolina.Desktop.exe') -ArgumentList ('"' + [IO.Path]::GetFullPath($ProjectPath) + '"') -WindowStyle Normal
