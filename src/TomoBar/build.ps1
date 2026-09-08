$ErrorActionPreference = 'Stop'
$base = $PSScriptRoot
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$out = Join-Path $base 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
$refs = @('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xaml.dll','System.Windows.Forms.dll','System.Drawing.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output',('/out:' + (Join-Path $out 'LittleTomato.exe')),('/win32manifest:' + (Join-Path $base 'app.manifest')))
if (Test-Path (Join-Path $base 'tomato.ico')) { $arguments += '/win32icon:' + (Join-Path $base 'tomato.ico'); $arguments += '/resource:' + (Join-Path $base 'tomato.ico') + ',tomato.ico' }
$arguments += '/resource:' + (Join-Path $base 'Theme.xaml') + ',Theme.xaml'
foreach ($ref in $refs) { $arguments += '/reference:' + (Join-Path $framework $ref) }
$arguments += @(Get-ChildItem -LiteralPath $base -Filter '*.cs' | ForEach-Object { $_.FullName })
& (Join-Path $framework 'csc.exe') $arguments
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Write-Output (Join-Path $out 'LittleTomato.exe')
