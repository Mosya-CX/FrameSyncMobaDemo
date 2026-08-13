$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$serverRoot = Join-Path $projectRoot 'Builds\UosServer'
$uploadRoot = Join-Path $projectRoot 'Builds\UosUpload'
$requiredFiles = @(
    (Join-Path $serverRoot 'FrameSyncMobaServer.x86_64'),
    (Join-Path $serverRoot 'UnityPlayer.so')
)
$dataRoot = Join-Path $serverRoot 'FrameSyncMobaServer_Data'

foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required UOS server file is missing: $requiredFile"
    }
}
if (-not (Test-Path -LiteralPath $dataRoot -PathType Container)) {
    throw "Required UOS server data directory is missing: $dataRoot"
}

New-Item -ItemType Directory -Path $uploadRoot -Force | Out-Null
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$baseName = "FrameSyncMobaServer_uos_$timestamp"
$archivePath = Join-Path $uploadRoot "$baseName.zip"
$suffix = 0
while (Test-Path -LiteralPath $archivePath) {
    $suffix++
    $archivePath = Join-Path $uploadRoot (
        '{0}-{1:00}.zip' -f $baseName, $suffix)
}
$temporaryPath = "$archivePath.tmp"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

try {
    $stream = [System.IO.File]::Open(
        $temporaryPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $stream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false)
        try {
            $files = Get-ChildItem -LiteralPath $serverRoot -Recurse -File |
                Where-Object {
                    $_.FullName -notmatch '_BurstDebugInformation_DoNotShip[\\/]'
                } |
                Sort-Object FullName
            foreach ($file in $files) {
                $relativePath = $file.FullName.Substring(
                    $serverRoot.Length).TrimStart('\', '/')
                $entryName = $relativePath.Replace('\', '/')
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive,
                    $file.FullName,
                    $entryName,
                    [System.IO.Compression.CompressionLevel]::Optimal) |
                    Out-Null
            }
        }
        finally {
            $archive.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    [System.IO.File]::Move($temporaryPath, $archivePath)
    $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumPath = "$archivePath.sha256"
    [System.IO.File]::WriteAllText(
        $checksumPath,
        "$hash  $([System.IO.Path]::GetFileName($archivePath))`r`n",
        [System.Text.UTF8Encoding]::new($false))

    Write-Host 'UOS upload package created:'
    Write-Host $archivePath
    Write-Host "SHA-256: $hash"
}
catch {
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
    throw
}
