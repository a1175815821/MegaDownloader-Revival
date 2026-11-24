param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory,
    [switch]$UiOnly,
    [switch]$StartupOnly,
    [switch]$NoBuild,
    [switch]$Screenshots,
    [string]$Cases,
    [string]$Category,
    [switch]$List
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}
$projectRoot = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $projectRoot ('bin\' + $Configuration)
if ($OutputDirectory) {
    $bin = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory)) }
}
Push-Location $projectRoot
try {
    if (-not $NoBuild) {
        & dotnet msbuild MegaDownloader.vbproj /t:Build "/p:Configuration=$Configuration" /p:Platform=x86 "/p:OutputPath=$bin\" /nologo /v:minimal
        if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
    } elseif (-not (Test-Path (Join-Path $bin 'MegaDownloader.exe'))) {
        throw "NoBuild requires an existing application build at $bin"
    }

    # Compile the test program once per (test sources + application binaries) content hash.
    # Repeat rounds against an unchanged build skip csc entirely; the exe is copied into a
    # fresh per-run directory so its run-* fixtures never pollute the cache.
    $appExe = Join-Path $bin 'MegaDownloader.exe'
    $refs = @('BouncyCastle.Cryptography.dll', 'HttpServer.dll', 'ObjectListView.dll') | ForEach-Object { Join-Path $bin $_ }
    $testSources = @('RegressionTests.cs', 'DeliveryAuditRegressionTests.cs', 'StartupPerformanceRegressionTests.cs', 'PublicSubscriptionRegressionTests.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
    $appConfig = Join-Path $projectRoot 'app.config'
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $accumulator = New-Object System.IO.MemoryStream
    foreach ($file in ($testSources + $appExe + $refs + $appConfig)) {
        $digest = $sha.ComputeHash([IO.File]::ReadAllBytes($file))
        $accumulator.Write($digest, 0, $digest.Length)
    }
    $key = ([BitConverter]::ToString($sha.ComputeHash($accumulator.ToArray())) -replace '-', '').Substring(0, 16)
    $accumulator.Dispose(); $sha.Dispose()
    $cacheDir = Join-Path $env:TEMP ('MegaDownloader-regression-cache\' + $key)
    $cachedExe = Join-Path $cacheDir 'RegressionTests.exe'
    if (-not (Test-Path $cachedExe)) {
        New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null
        $compilerArgs = @(
            '/nologo', '/target:exe',
            ('/out:' + $cachedExe),
            ('/r:' + $appExe),
            ('/r:' + (Join-Path $bin 'BouncyCastle.Cryptography.dll')),
            ('/r:' + (Join-Path $bin 'HttpServer.dll')),
            ('/r:' + (Join-Path $bin 'ObjectListView.dll')),
            '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll',
            '/r:System.Windows.Forms.dll',
            '/r:System.Drawing.dll'
        ) + $testSources
        & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" @compilerArgs
        if ($LASTEXITCODE -ne 0) { throw 'Regression test compilation failed.' }
        Write-Host "Compiled regression tests (cache $key)."
    } else {
        Write-Host "Reusing compiled regression tests (cache $key)."
    }

    $testRoot = Join-Path $env:TEMP ('MegaDownloader-regression-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    Copy-Item -LiteralPath $cachedExe -Destination (Join-Path $testRoot 'RegressionTests.exe')
    Copy-Item -LiteralPath $appConfig -Destination (Join-Path $testRoot 'RegressionTests.exe.config')
    $testArgs = @($projectRoot, $bin)
    if ($UiOnly) { $testArgs += '--ui-only' }
    if ($StartupOnly) { $testArgs += '--startup-only' }
    if ($Screenshots) { $testArgs += '--screenshots' }
    if ($Cases) { $testArgs += ('--cases=' + $Cases) }
    if ($Category) { $testArgs += ('--category=' + $Category) }
    if ($List) { $testArgs += '--list' }
    & (Join-Path $testRoot 'RegressionTests.exe') @testArgs | Tee-Object -FilePath (Join-Path $testRoot 'results.txt')
    if ($LASTEXITCODE -ne 0) { throw ('Regression tests failed. See ' + $testRoot) }
} finally {
    Pop-Location
}