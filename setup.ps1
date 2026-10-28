param([Parameter(Mandatory=$true)][string]$MPUltDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$appRoot = (Resolve-Path -LiteralPath $MPUltDirectory).Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework 4.x C# compiler was not found. This build is tested on 64-bit Windows.' }
$dllNames = @('Microsoft.DirectX.dll','Microsoft.DirectX.Direct3D.dll','Microsoft.DirectX.Direct3DX.dll')
foreach ($dllName in $dllNames) {
    $dependency = Join-Path $appRoot $dllName
    if (-not (Test-Path -LiteralPath $dependency)) { throw "Missing app dependency: $dependency" }
    Copy-Item -LiteralPath $dependency -Destination (Join-Path $repoRoot $dllName)
}
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'engine') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
foreach ($folder in @('src','lib','s3d')) {
    $sourceFiles += @(Get-ChildItem -LiteralPath (Join-Path $repoRoot "vendor\mpult\$folder") -Filter '*.cs' | Select-Object -ExpandProperty FullName)
}
$compileArgs = @('/nologo','/unsafe','/optimize','/target:exe','/main:_3dedit.MoveEngine',('/out:' + (Join-Path $repoRoot 'MoveEngine.exe')),'/r:System.Drawing.dll','/r:System.Windows.Forms.dll')
foreach ($dllName in $dllNames) { $compileArgs += '/r:' + (Join-Path $repoRoot $dllName) }
$compileArgs += $sourceFiles
& $compiler @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Native engine compilation failed.' }
Write-Output 'Build complete. Run: python doctor.py --engine'
