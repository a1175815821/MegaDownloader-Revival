param(
    [string]$BuildOutput = 'bin\Release',
    [string]$Destination = 'artifacts',
    [switch]$TestStartup
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$build = (Resolve-Path -LiteralPath $BuildOutput).Path
$stage = (New-Item -ItemType Directory -Path $Destination -Force).FullName
$exeName = 'MegaDownloader.exe'
$configName = "$exeName.config"
$licenseName = 'MEGA-SDK-LICENSE.txt'
$nativeRoot = Join-Path $projectRoot 'Native\MegaAccount\prebuilt\x64'
$nativeNames = @('MegaAccountBridge.dll', 'msvcp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll')
$exe = Join-Path $stage $exeName
$zip = Join-Path $stage 'MegaDownloader-Revival-win-x86.zip'
$config = Join-Path $build $configName
$license = Join-Path $nativeRoot $licenseName

Copy-Item -LiteralPath (Join-Path $build $exeName) -Destination $exe -Force
Compress-Archive -LiteralPath $exe, $config, $license -DestinationPath $zip -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    if ($archive.Entries.Count -ne 3) { throw "Unexpected ZIP contents: $($archive.Entries.FullName -join ', ')" }
    foreach ($file in @($exe, $config, $license)) {
        $entry = $archive.GetEntry([IO.Path]::GetFileName($file))
        if ($null -eq $entry) { throw "Missing ZIP entry for $file" }
        $stream = $entry.Open()
        try {
            if ((Get-FileHash -InputStream $stream).Hash -ne (Get-FileHash -LiteralPath $file).Hash) {
                throw "ZIP entry differs from $file"
            }
        } finally { $stream.Dispose() }
    }
} finally { $archive.Dispose() }

# Use Windows PowerShell so the staged .NET Framework executable loads in its native runtime.
$assembly = [Reflection.Assembly]::LoadFrom($exe)
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'MegaDownloader.vbproj') -Raw
$managedFiles = @($project.Project.ItemGroup.Reference | Where-Object HintPath | ForEach-Object {
    Join-Path $projectRoot $_.HintPath
})
$dllFiles = $managedFiles + @($nativeNames | ForEach-Object { Join-Path $nativeRoot $_ })
foreach ($file in $dllFiles) {
    $name = [IO.Path]::GetFileName($file)
    $resource = $assembly.GetManifestResourceStream("MegaDownloader.Dependencies.$name.gz")
    if ($null -eq $resource) { throw "Missing embedded DLL: $name" }
    $stream = [IO.Compression.GZipStream]::new($resource, [IO.Compression.CompressionMode]::Decompress)
    try {
        if ((Get-FileHash -InputStream $stream).Hash -ne (Get-FileHash -LiteralPath $file).Hash) {
            throw "Embedded DLL differs from $file"
        }
    } finally { $stream.Dispose() }
}
$notice = $assembly.GetManifestResourceStream("MegaDownloader.$licenseName")
if ($null -eq $notice) { throw "Missing embedded SDK license" }
try {
    if ((Get-FileHash -InputStream $notice).Hash -ne (Get-FileHash -LiteralPath $license).Hash) {
        throw "Embedded SDK license differs from $license"
    }
} finally { $notice.Dispose() }

$static = [Reflection.BindingFlags]'Static, NonPublic'
$appType = $assembly.GetType('MegaDownloader.My.MyApplication', $true)
$app = [Activator]::CreateInstance($appType, $true)
$resolver = [Delegate]::CreateDelegate([ResolveEventHandler], $app,
    $appType.GetMethod('LoadDLLFromStream', [Reflection.BindingFlags]'Instance, NonPublic'))
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    $service = $assembly.GetType('MegaDownloader.MegaAccountService', $true)
    $availability = $service.GetMethod('Availability', $static).Invoke($null, $null)
    if ($availability -ne 0) { throw "Packaged MEGA account module failed to load: $availability" }
    $native = $assembly.GetType('MegaDownloader.MegaAccountNative', $true)
    $cache = [IO.Path]::Combine([IO.Path]::GetTempPath(), 'MegaDownloader-sdk-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $cache | Out-Null
    try {
        $sessionHandle = $native.GetMethod('md_open', $static).Invoke($null, [string[]]@('', $cache, '', '', ''))
        try {
            if ($sessionHandle.IsInvalid) { throw 'Packaged MEGA SDK could not open a client.' }
        } finally { $sessionHandle.Dispose() }
    } finally { Remove-Item -LiteralPath $cache -Recurse -Force }

    $dialog = [Activator]::CreateInstance($assembly.GetType('MegaDownloader.MegaAccountForm', $true), $true)
    try {
        $signIn = $dialog.Controls.Find('MegaAccountAction', $true)
        if ($signIn.Count -ne 1 -or -not $signIn[0].Enabled) { throw 'Packaged MEGA sign-in button is unavailable.' }
    } finally { $dialog.Dispose() }
} finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }

Write-Host "Verified $($dllFiles.Count) embedded DLLs, MEGA SDK initialization, sign-in dialog and ZIP contents."

function Test-MainWindow([string]$Path) {
    $process = Start-Process -FilePath $Path -WorkingDirectory (Split-Path -Parent $Path) -PassThru
    try {
        $timeout = [Diagnostics.Stopwatch]::StartNew()
        do {
            $process.Refresh()
            if ($process.HasExited) { throw "Application exited before showing its main window: $Path ($($process.ExitCode))" }
            if ($process.MainWindowTitle -match '^MegaDownloader v\d') {
                Write-Host "Started: $Path ($($process.MainWindowTitle))"
                return
            }
            Start-Sleep -Milliseconds 200
        } while ($timeout.Elapsed.TotalSeconds -lt 30)
        throw "Application did not show its main window: $Path"
    } finally {
        if (-not $process.HasExited) {
            if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(2000)) {
                $process.Kill()
                $process.WaitForExit()
            }
        }
        $process.Dispose()
    }
}

if ($TestStartup) {
    Test-MainWindow $exe
    $unpacked = Join-Path ([IO.Path]::GetTempPath()) ('MegaDownloader-release-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $unpacked | Out-Null
    try {
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $unpacked)
        Test-MainWindow (Join-Path $unpacked $exeName)
    } finally { Remove-Item -LiteralPath $unpacked -Recurse -Force }
}

Get-Item -LiteralPath $exe, $zip | Select-Object Name, Length
