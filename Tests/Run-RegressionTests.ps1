param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $env:TEMP ('MegaDownloader-regression-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$bin = Join-Path $projectRoot ('bin\' + $Configuration)
Push-Location $projectRoot
try {
    & dotnet msbuild MegaDownloader.vbproj /t:Build "/p:Configuration=$Configuration" /p:Platform=x86 /nologo /v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
    $compilerArgs = @(
        '/nologo', '/target:exe',
        ('/out:' + (Join-Path $testRoot 'RegressionTests.exe')),
        ('/r:' + (Join-Path $bin 'MegaDownloader.exe')),
        ('/r:' + (Join-Path $bin 'BouncyCastle.Cryptography.dll')),
        ('/r:' + (Join-Path $bin 'HttpServer.dll')),
        '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll',
        '/r:System.Windows.Forms.dll',
        '/r:System.Drawing.dll',
        (Join-Path $PSScriptRoot 'RegressionTests.cs')
    )
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" @compilerArgs
    if ($LASTEXITCODE -ne 0) { throw 'Regression test compilation failed.' }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'app.config') -Destination (Join-Path $testRoot 'RegressionTests.exe.config')
    & (Join-Path $testRoot 'RegressionTests.exe') $projectRoot $bin | Tee-Object -FilePath (Join-Path $testRoot 'results.txt')
    if ($LASTEXITCODE -ne 0) { throw ('Regression tests failed. See ' + $testRoot) }
} finally {
    Pop-Location
}
