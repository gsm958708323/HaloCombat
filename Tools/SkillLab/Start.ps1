param(
    [switch]$SkipBuild,
    [ValidateRange(1024,65535)][int]$Port=4192,
    [switch]$NoBrowser
)
$ErrorActionPreference = 'Stop'
$toolDirectory = $PSScriptRoot
$repositoryDirectory = [System.IO.Path]::GetFullPath((Join-Path $toolDirectory '../..'))
$url = "http://127.0.0.1:$Port/"
function Open-RunningSkillLab {
    $listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
    if (-not ($listeners | Where-Object { $_.Port -eq $Port })) { return $false }
    try {
        $document = Invoke-RestMethod -Uri ($url+'api/project') -TimeoutSec 3
        if ($document.formatVersion -ne 1 -or $document.skills -isnot [array] -or $null -eq $document.preview) {
            throw 'Unexpected service.'
        }
    } catch {
        throw "Port $Port is occupied by another service or SkillLab is not responding. Retry later or use -Port 4193."
    }
    Write-Host "SkillLab is already running: $url"
    if (-not $NoBrowser) { Start-Process $url }
    return $true
}

if (Open-RunningSkillLab) { return }
$hasher = [System.Security.Cryptography.SHA256]::Create()
try { $rootHash = [BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($repositoryDirectory.ToLowerInvariant()))).Replace('-','') }
finally { $hasher.Dispose() }
# Every port shares the same build outputs. Hold this lock until the new host is ready.
$startupLock = [System.Threading.Mutex]::new($false,('Local\HaloCombat.SkillLab.Start.'+$rootHash))
$lockHeld = $false
$hostProcess = $null
Push-Location $repositoryDirectory
try {
    try {
        if (-not $startupLock.WaitOne(0)) {
            Write-Host 'Another SkillLab launch is building. Waiting for it to finish...'
            $null = $startupLock.WaitOne()
        }
        $lockHeld = $true
    } catch [System.Threading.AbandonedMutexException] { $lockHeld = $true }
    if (Open-RunningSkillLab) { return }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET SDK 10 before starting SkillLab.' }
    if (-not $SkipBuild) {
        if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) { throw 'Install Node.js 22 before starting SkillLab.' }
        Push-Location (Join-Path $toolDirectory 'Web')
        try {
            if (-not (Test-Path -LiteralPath 'node_modules')) { npm.cmd ci --no-audit --no-fund; if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed.' } }
            npm.cmd run build
            if ($LASTEXITCODE -ne 0) { throw 'Web build failed.' }
        } finally { Pop-Location }
        dotnet build (Join-Path $toolDirectory 'SkillLab.Host.csproj') -v q --nologo
        if ($LASTEXITCODE -ne 0) { throw 'C# host build failed.' }
    }
    $hostAssembly = Join-Path $toolDirectory 'bin/Debug/net10.0/SkillLab.Host.dll'
    if (-not (Test-Path -LiteralPath $hostAssembly) -or -not (Test-Path -LiteralPath (Join-Path $toolDirectory 'Web/dist/index.html'))) {
        throw 'Build outputs are missing. Start again without -SkipBuild.'
    }
    Write-Host "SkillLab: $url"
    Write-Host 'Keep this window open. Press Ctrl+C to stop the server.'
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = (Get-Command dotnet).Source
    $startInfo.Arguments = '"{0}" --root "{1}" --port {2}' -f $hostAssembly,$repositoryDirectory,$Port
    $startInfo.WorkingDirectory = $repositoryDirectory
    $startInfo.UseShellExecute = $false
    $hostProcess = [System.Diagnostics.Process]::Start($startInfo)
    $ready = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while (-not $hostProcess.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        try {
            $page = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 1
            if ($page.StatusCode -eq 200) { $ready = $true; break }
        } catch {}
        Start-Sleep -Milliseconds 200
    }
    if (-not $ready) { throw 'SkillLab did not become ready. See the host error above.' }
    $startupLock.ReleaseMutex(); $lockHeld = $false
    if (-not $NoBrowser) { Start-Process $url }
    while (-not $hostProcess.WaitForExit(250)) {}
    if ($hostProcess.ExitCode -ne 0) { throw 'Skill Lab host stopped with an error.' }
} finally {
    if ($null -ne $hostProcess) {
        if (-not $hostProcess.HasExited) { $hostProcess.Kill() }
        $hostProcess.Dispose()
    }
    if ($lockHeld) { $startupLock.ReleaseMutex() }
    $startupLock.Dispose()
    Pop-Location
}
