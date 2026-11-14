param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$toolDirectory = $PSScriptRoot
$repositoryDirectory = [System.IO.Path]::GetFullPath((Join-Path $toolDirectory '../..'))
Push-Location $repositoryDirectory
try {
    if (-not $SkipBuild) {
        Push-Location (Join-Path $toolDirectory 'Web')
        try {
            if (-not (Test-Path -LiteralPath 'node_modules')) { npm.cmd ci --no-audit --no-fund; if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed.' } }
            npm.cmd run build
            if ($LASTEXITCODE -ne 0) { throw 'Web build failed.' }
        } finally { Pop-Location }
        dotnet build (Join-Path $toolDirectory 'SkillLab.Host.csproj') -v q --nologo
        if ($LASTEXITCODE -ne 0) { throw 'C# host build failed.' }
    }
    dotnet (Join-Path $toolDirectory 'bin/Debug/net10.0/SkillLab.Host.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Skill Lab host stopped with an error.' }
} finally { Pop-Location }
