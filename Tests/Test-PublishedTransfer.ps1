# Run a published Windows build against a permitted single-file link and its known SHA-256.
# The process gets its own profile and output folder. No desktop clicks are required.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Executable,
    [Parameter(Mandatory)][string] $Link,
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{64}$')][string] $Sha256,
    [ValidateRange(15,3600)][int] $TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
$binary = (Resolve-Path -LiteralPath $Executable).Path
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('MegaTransferCheck-' + [Guid]::NewGuid().ToString('N'))
$profilePath = Join-Path $scratch 'Profile'
$settingsPath = Join-Path $profilePath 'Config'
$outputPath = Join-Path $scratch 'Downloads'
[void][IO.Directory]::CreateDirectory($settingsPath)

$config = [xml]'<Configuration><RutaDefecto/><AnalizarPortapapeles>False</AnalizarPortapapeles><CrearDirectorioPaquete>False</CrearDirectorioPaquete><ExtraerAutomaticamente>False</ExtraerAutomaticamente><ServidorWebActivo>False</ServidorWebActivo><ServidorStreamingActivo>False</ServidorStreamingActivo></Configuration>'
$config.DocumentElement.SelectSingleNode('RutaDefecto').InnerText = $outputPath
$config.Save((Join-Path $settingsPath 'Configuration.xml'))
[IO.File]::WriteAllText((Join-Path $profilePath 'ui-settings.json'), '{"HasSeenGuide":true}')
$queuePath = Join-Path $settingsPath 'DownloadList.xml'

$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = $binary
$start.WorkingDirectory = Split-Path -Parent $binary
$start.UseShellExecute = $false
if ($Link -match '[\s"]') { throw 'Supply one MEGA link.' }
$start.Arguments = $Link
$start.EnvironmentVariables['MEGADOWNLOADER_DATA_DIR'] = $profilePath
$passed = $false
$app = [Diagnostics.Process]::Start($start)
try {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 400
        if ($app.HasExited) { throw "Application exited with code $($app.ExitCode)." }
        if (-not [IO.File]::Exists($queuePath)) { continue }
        try { $queue = [xml][IO.File]::ReadAllText($queuePath) } catch [IO.IOException] { continue }
        $tasks = @($queue.SelectNodes('//ListaFicheros/Fichero'))
        if ($tasks.Count -eq 0) { continue }
        if ($tasks.Count -ne 1) { throw "Expected one file, got $($tasks.Count)." }
        $file = $tasks[0]
        if ($file.EstadoDescarga -eq 'Erroneo') { throw 'Transfer entered the error state.' }
        if ($file.EstadoDescarga -ne 'Completado') { continue }
        if ([decimal]::Parse($file.Porcentaje,[CultureInfo]::CurrentCulture) -ne 100) {
            continue
        }
        $outputs = @(Get-ChildItem -LiteralPath $outputPath -File -Recurse)
        if ($outputs.Count -ne 1 -or $outputs[0].Extension -eq '.part') { throw 'Expected one finalized output file.' }
        if ($outputs[0].Length -ne [long]$file.TamanoBytes) { throw 'Output length differs from queue metadata.' }
        if ((Get-FileHash -LiteralPath $outputs[0].FullName -Algorithm SHA256).Hash -ne $Sha256) { throw 'Output SHA-256 mismatch.' }
        $passed = $true
        Write-Output "PASS · $($outputs[0].Length) bytes · queue complete · SHA-256 verified"
        return
    }
    throw "Transfer did not complete within $TimeoutSeconds seconds."
}
finally {
    if (-not $app.HasExited) {
        [void]$app.CloseMainWindow()
        if (-not $app.WaitForExit(8000)) { $app.Kill(); $app.WaitForExit() }
    }
    $app.Dispose()
    $resolved = [IO.Path]::GetFullPath($scratch)
    if ($passed -and [IO.Path]::GetDirectoryName($resolved) -eq [IO.Path]::GetTempPath().TrimEnd('\') -and
        [IO.Path]::GetFileName($resolved) -match '^MegaTransferCheck-[a-f0-9]{32}$') {
        Remove-Item -LiteralPath $resolved -Force -Recurse
    } else {
        Write-Warning "Isolated transfer files retained in $scratch"
    }
}
