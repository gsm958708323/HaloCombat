param(
    [switch]$SkipBuild,
    [ValidateRange(1024,65535)][int]$Port=4192,
    [switch]$NoBrowser,
    [switch]$Rebuild
)
$ErrorActionPreference = 'Stop'
$toolDirectory = $PSScriptRoot
$repositoryDirectory = [System.IO.Path]::GetFullPath((Join-Path $toolDirectory '../..'))
$url = "http://127.0.0.1:$Port/"
. (Join-Path $toolDirectory 'Start.Build.ps1')
function Confirm-SkillLabReady {
    try {
        $document = Invoke-RestMethod -Uri ($url+'api/project') -TimeoutSec 3
        if ($document.formatVersion -ne 1 -or $document.skills -isnot [array] -or $null -eq $document.preview) {
            throw 'Unexpected service.'
        }
    } catch {
        throw "Port $Port is occupied by another service or SkillLab is not responding. Retry later or use -Port 4193."
    }
}

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
    $runningProcess = Get-SkillLabRunningProcess -ServerPort $Port -Root $repositoryDirectory
    if ($runningProcess) { Confirm-SkillLabReady }
    $stampFile = Join-Path $toolDirectory '.local/launch-build.json'
    $sourceFingerprint = Get-SkillLabSourceFingerprint -Root $repositoryDirectory
    $artifactFingerprint = Get-SkillLabArtifactFingerprint -Root $repositoryDirectory
    $savedStamp = $null
    if (Test-Path -LiteralPath $stampFile) { try { $savedStamp = Get-Content -LiteralPath $stampFile -Raw | ConvertFrom-Json } catch {} }
    $needsBuild = -not $SkipBuild -and ($Rebuild -or -not $artifactFingerprint -or $savedStamp.source -ne $sourceFingerprint -or $savedStamp.artifacts -ne $artifactFingerprint)
    if ($runningProcess -and -not $needsBuild -and -not $Rebuild) {
        Write-Host "SkillLab is already running with the current build: $url"
        Open-SkillLabPage -BaseUrl $url -Disabled:$NoBrowser
        return
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET SDK 10 before starting SkillLab.' }
    if ($needsBuild) {
        Write-Host 'Source or build output changed. Building the current editor...'
        if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) { throw 'Install Node.js 22 before starting SkillLab.' }
        Push-Location (Join-Path $toolDirectory 'Web')
        try {
            if (-not (Test-Path -LiteralPath 'node_modules')) { npm.cmd ci --no-audit --no-fund; if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed.' } }
            npm.cmd run build
            if ($LASTEXITCODE -ne 0) { throw 'Web build failed.' }
        } finally { Pop-Location }
    }
    if ($runningProcess) {
        # Recheck ownership under the repository startup lock before stopping it.
        $currentProcess = Get-SkillLabRunningProcess -ServerPort $Port -Root $repositoryDirectory
        if ($currentProcess -and $currentProcess.ProcessId -eq $runningProcess.ProcessId -and $currentProcess.CreationDate -eq $runningProcess.CreationDate) {
            Stop-Process -Id $currentProcess.ProcessId
            $stoppingProcess = Get-Process -Id $currentProcess.ProcessId -ErrorAction SilentlyContinue
            if ($stoppingProcess) { $null = $stoppingProcess.WaitForExit(5000) }
        }
    }
    if ($needsBuild) {
        dotnet build (Join-Path $toolDirectory 'SkillLab.Host.csproj') -v q --nologo
        if ($LASTEXITCODE -ne 0) { throw 'C# host build failed.' }
        $null = New-Item -ItemType Directory -Path (Split-Path $stampFile) -Force
        @{source=$sourceFingerprint;artifacts=(Get-SkillLabArtifactFingerprint -Root $repositoryDirectory)} | ConvertTo-Json | Set-Content -LiteralPath $stampFile -Encoding UTF8
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
    Open-SkillLabPage -BaseUrl $url -Disabled:$NoBrowser
    while (-not $hostProcess.WaitForExit(250)) {}
    if ($hostProcess.ExitCode -ne 0) {
        # A newer launch may replace this process while holding the same mutex.
        # Let that launch finish before treating its intentional stop as failure.
        $replacementLock = $false
        try {
            $replacementLock = $startupLock.WaitOne(0)
            if (-not $replacementLock) { $replacementLock = $startupLock.WaitOne(30000) }
            $replacement = Get-SkillLabRunningProcess -ServerPort $Port -Root $repositoryDirectory
            if (-not $replacement -or $replacement.ProcessId -eq $hostProcess.Id) { throw 'Skill Lab host stopped with an error.' }
            Write-Host 'This host was replaced by a newer SkillLab launch.'
        } finally { if ($replacementLock) { $startupLock.ReleaseMutex() } }
    }
} finally {
    if ($null -ne $hostProcess) {
        if (-not $hostProcess.HasExited) { $hostProcess.Kill() }
        $hostProcess.Dispose()
    }
    if ($lockHeld) { $startupLock.ReleaseMutex() }
    $startupLock.Dispose()
    Pop-Location
}
