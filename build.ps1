[CmdletBinding()]
param([switch]$Test, [switch]$Package)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'src\TomoBar\build.ps1')
$exe = Join-Path $PSScriptRoot 'src\TomoBar\bin\LittleTomato.exe'
$versionInfo = (Get-Item -LiteralPath $exe).VersionInfo
$version = '{0}.{1}.{2}' -f $versionInfo.FileMajorPart, $versionInfo.FileMinorPart, $versionInfo.FileBuildPart
if ($Test) {
    $reports = Join-Path $PSScriptRoot 'artifacts'
    New-Item -ItemType Directory -Force $reports | Out-Null
    $report = Join-Path $reports 'core-tests.txt'
    $process = Start-Process -FilePath $exe -ArgumentList '--self-test', ('"' + $report + '"') -WindowStyle Hidden -PassThru -Wait
    Get-Content -LiteralPath $report
    if ($process.ExitCode -ne 0) { throw 'Core tests failed.' }
}
if ($Package) {
    $dist = Join-Path $PSScriptRoot 'dist'
    $stage = Join-Path $dist ('stage-' + [Guid]::NewGuid().ToString('N'))
    $folder = Join-Path $stage 'TomoBar'
    New-Item -ItemType Directory -Force $folder | Out-Null
    Copy-Item -LiteralPath $exe -Destination (Join-Path $folder '小番茄.exe')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\USER_GUIDE.md') -Destination (Join-Path $folder '使用说明.md')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CHANGELOG.md') -Destination $folder
    Copy-Item -LiteralPath $exe -Destination (Join-Path $dist 'TomoBar.exe') -Force
    $zip = Join-Path $dist "TomoBar-$version-win-x64.zip"
    Compress-Archive -LiteralPath $folder -DestinationPath $zip -Force
    $hashes = foreach ($file in @((Join-Path $dist 'TomoBar.exe'), $zip)) {
        $hash = Get-FileHash -LiteralPath $file -Algorithm SHA256
        $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($file)
    }
    [IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'), [string[]]$hashes, [Text.UTF8Encoding]::new($false))
    Write-Output "Packaged TomoBar $version in $dist"
}
