# Build fingerprints exclude user data, publications, generated output and dependencies.
function Get-SkillLabFileHash {
    param([string]$Path)
    $stream = [IO.File]::OpenRead($Path)
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-','') }
    finally { $stream.Dispose(); $hasher.Dispose() }
}

function Get-SkillLabSourceFingerprint {
    param([string]$Root)
    $files = @()
    foreach ($relative in @('Assets/Scripts/Combat/Core','Assets/Scripts/Combat/Config/Json','Tools/SkillLab/Host')) {
        $files += Get-ChildItem -LiteralPath (Join-Path $Root $relative) -Filter '*.cs' -File -Recurse
    }
    foreach ($relative in @('Tools/SkillLab/Web/src','Tools/SkillLab/Web/public')) {
        $folder = Join-Path $Root $relative
        if (Test-Path -LiteralPath $folder) { $files += Get-ChildItem -LiteralPath $folder -File -Recurse }
    }
    $files += Get-Item -LiteralPath (Join-Path $Root 'Tools/SkillLab/SkillLab.Host.csproj')
    $files += Get-ChildItem -LiteralPath (Join-Path $Root 'Tools/SkillLab/Web') -File | Where-Object { $_.Name -match '^(package.*\.json|tsconfig.*\.json|vite\.config\..*|index\.html)$' }
    $files += Get-ChildItem -LiteralPath $Root -File | Where-Object { $_.Name -match '^(Directory\.(Build|Packages)\..*|global\.json|NuGet\.Config)$' }
    $parts = $files | Sort-Object FullName -Unique | ForEach-Object {
        $_.FullName.Substring($Root.Length).Replace('\','/') + ':' + (Get-SkillLabFileHash -Path $_.FullName)
    }
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes(($parts -join "`n")))).Replace('-','') }
    finally { $hasher.Dispose() }
}

function Get-SkillLabArtifactFingerprint {
    param([string]$Root)
    $assembly = Join-Path $Root 'Tools/SkillLab/bin/Debug/net10.0/SkillLab.Host.dll'
    $index = Join-Path $Root 'Tools/SkillLab/Web/dist/index.html'
    if (-not (Test-Path -LiteralPath $assembly) -or -not (Test-Path -LiteralPath $index)) { return '' }
    $files = @((Get-Item -LiteralPath $assembly)) + @(Get-ChildItem -LiteralPath (Split-Path $index) -File -Recurse)
    return (($files | Sort-Object FullName | ForEach-Object { $_.Name + ':' + (Get-SkillLabFileHash -Path $_.FullName) }) -join '|')
}

function Get-SkillLabRunningProcess {
    param([int]$ServerPort,[string]$Root)
    $listener = Get-NetTCPConnection -LocalPort $ServerPort -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $listener) { return $null }
    $process = Get-CimInstance Win32_Process -Filter ('ProcessId='+$listener.OwningProcess)
    $expected = (Join-Path $Root 'Tools/SkillLab/bin/Debug/net10.0/SkillLab.Host.dll').Replace('/','\')
    $command = ([string]$process.CommandLine).Replace('/','\')
    if ($process.Name -ne 'dotnet.exe' -or $command -notmatch ('(?i)(?:^|\s)"?'+[regex]::Escape($expected)+'"?(?=\s|$)')) {
        throw "Port $ServerPort belongs to a different program or checkout. Use another -Port."
    }
    return $process
}

function Open-SkillLabPage {
    param([string]$BaseUrl,[switch]$Disabled)
    if (-not $Disabled) { Start-Process ($BaseUrl+'?launch='+[DateTime]::UtcNow.Ticks) }
}
