param([string]$OutputDirectory='', [switch]$SkipWebBuild)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $PSScriptRoot ('.local/releases/SkillLab-'+(Get-Date -Format 'yyyyMMdd-HHmmss')) }
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $taskOutput) { throw "Output already exists. Choose a new folder: $taskOutput" }
if (-not $SkipWebBuild) {
    Push-Location (Join-Path $PSScriptRoot 'Web')
    try {
        if (-not (Test-Path -LiteralPath 'node_modules')) { npm.cmd ci --no-audit --no-fund; if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' } }
        npm.cmd run build; if ($LASTEXITCODE -ne 0) { throw 'Web build failed.' }
    } finally { Pop-Location }
}
$taskDist=Join-Path $PSScriptRoot 'Web/dist'
if (-not (Test-Path -LiteralPath (Join-Path $taskDist 'index.html'))) { throw 'Web build is missing.' }
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskHost=Join-Path $taskOutput 'Tools/SkillLab/HostRuntime'
dotnet publish (Join-Path $PSScriptRoot 'SkillLab.Host.csproj') -c Release -r win-x64 --self-contained true -o $taskHost --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Host publish failed. The partial folder is retained for diagnosis.' }
foreach ($taskRelative in @('Content/SkillLab','Assets/StreamingAssets/Combat','Tools/SkillLab/Web/dist')) { New-Item -ItemType Directory -Force -Path (Join-Path $taskOutput $taskRelative) | Out-Null }
Copy-Item -LiteralPath (Join-Path $taskRoot 'Content/SkillLab/project.json') -Destination (Join-Path $taskOutput 'Content/SkillLab/project.json')
Copy-Item -LiteralPath (Join-Path $taskRoot 'Assets/StreamingAssets/Combat/project.json') -Destination (Join-Path $taskOutput 'Assets/StreamingAssets/Combat/project.json')
foreach($taskRelative in @('Content/SkillLab','Assets/StreamingAssets/Combat')) {
    $taskSource=Join-Path $taskRoot $taskRelative
    $taskIndex=Get-Content -LiteralPath (Join-Path $taskSource 'project.json') -Raw -Encoding utf8 | ConvertFrom-Json
    $taskRefs=@($taskIndex.files.PSObject.Properties | ForEach-Object {
        if($_.Name -eq 'runtime'){$_.Value.PSObject.Properties.Value}else{$_.Value}
    })
    foreach($taskRef in $taskRefs){
        $taskTarget=Join-Path $taskOutput "$taskRelative/$taskRef"
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $taskTarget) | Out-Null
        Copy-Item -LiteralPath (Join-Path $taskSource $taskRef) -Destination $taskTarget
    }
}
Get-ChildItem -LiteralPath $taskDist | Copy-Item -Destination (Join-Path $taskOutput 'Tools/SkillLab/Web/dist') -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $taskOutput 'DEVELOPMENT.md')
$taskSourceDocs=Join-Path $taskOutput 'docs'
New-Item -ItemType Directory -Path $taskSourceDocs | Out-Null
foreach ($taskRelative in @('网页技能编辑器功能清单.md','网页节点与运行时映射.md','实施进度.md','JSON协议.md','优化实施-2026-11-16.md','需求复查-2026-11-16.md','技术债务.md')) { Copy-Item -LiteralPath (Join-Path $taskRoot "docs/$taskRelative") -Destination (Join-Path $taskSourceDocs $taskRelative) }
if (Test-Path -LiteralPath (Join-Path $taskRoot 'docs/最终验收.md')) { Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/最终验收.md') -Destination $taskSourceDocs }
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/adr') -Destination $taskSourceDocs -Recurse
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/验证') -Destination $taskSourceDocs -Recurse
$taskStart=@'
param([int]$Port=4192,[string]$PublishDirectory='')
$ErrorActionPreference='Stop'
$taskPackageRoot=$PSScriptRoot
if (-not $PublishDirectory) { $PublishDirectory=Join-Path $taskPackageRoot 'Assets/StreamingAssets/Combat' }
$taskExecutable=Join-Path $taskPackageRoot 'Tools/SkillLab/HostRuntime/SkillLab.Host.exe'
Write-Host "SkillLab: http://127.0.0.1:$Port/"
Write-Host "Data stays in this package. To publish into Unity, pass -PublishDirectory <project>/Assets/StreamingAssets/Combat."
& $taskExecutable --root $taskPackageRoot --port $Port --publish-directory $PublishDirectory
if ($LASTEXITCODE -ne 0) { throw 'SkillLab stopped. If the port is in use, start with -Port 4193.' }
'@
[IO.File]::WriteAllText((Join-Path $taskOutput 'Start.ps1'),$taskStart,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $taskOutput 'Run.cmd'),"@echo off`r`npowershell.exe -NoProfile -ExecutionPolicy Bypass -File `"%~dp0Start.ps1`"`r`npause`r`n",[Text.ASCIIEncoding]::new())
$taskReadme=@'
# SkillLab Windows 试玩包

本包包含网页、C#宿主和所需.NET运行时，Windows x64无需安装Node.js/.NET，也不要求打开Unity。

1. 解压到可写目录，双击Run.cmd，保持终端开启。
2. 浏览器打开 http://127.0.0.1:4192/ ，新建技能后“施放”并“播放”。
3. 如果4192已使用，可在PowerShell运行 ./Start.ps1 -Port 4193。

工作工程保存在本包Tools/SkillLab/.local/project.json，示例与正式JSON独立保留，不会自动覆盖已有工作。
工程可下载、回读、合并、撤销。导入支持选择文件或粘贴JSON。F6/F7默认发布到包内Assets/StreamingAssets/Combat，不能直接启动Unity游戏。
要发布到现有Unity工程，运行 ./Start.ps1 -PublishDirectory 'D:/your-project/Assets/StreamingAssets/Combat'，再到Unity SkillLab按F5重载；Unity项目与原Prefab/动画/材质随源工程交付，本包不会复制或替换它们。
“发布正式内容”写包内正式JSON；若要更新源工程，请导出完整运行内容并按源工程约定放置。不要用示例工程替换正式Arena内容。

53个模板与全部已定义技能模块可使用。装备、公式编辑器、独立地图、冒险不在范围；原始资源未提供音频，不宣称包含原音效。
docs/包含JSON映射、完整范围、ADR与验收记录，DEVELOPMENT.md说明源码开发流程。manifest.json保存本包来源提交及文件校验值。
'@
[IO.File]::WriteAllText((Join-Path $taskOutput 'README.md'),$taskReadme,[Text.UTF8Encoding]::new($false))
$taskRevision=git -C $taskRoot rev-parse HEAD
$taskRelevant=@('Assets/Scripts/Combat','Tools/SkillLab/Host','Tools/SkillLab/Web/src','Tools/SkillLab/Package.ps1','Content/SkillLab','Assets/StreamingAssets/Combat/project.json','Assets/StreamingAssets/Combat/preview.json')
foreach($taskPart in @('skills','projectiles','areas','buffs','timelines','summonBlueprints','runtime')){$taskRelevant+='Assets/StreamingAssets/Combat/'+$taskPart}
$taskHashes=Get-ChildItem -LiteralPath $taskOutput -File -Recurse | ForEach-Object { @{path=[IO.Path]::GetRelativePath($taskOutput,$_.FullName).Replace([IO.Path]::DirectorySeparatorChar,'/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} }
@{sourceRevision=$taskRevision;sourceDirty=[bool](git -C $taskRoot status --porcelain -- $taskRelevant);createdAt=(Get-Date).ToString('o');runtime='win-x64 self-contained';files=@($taskHashes)} | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $taskOutput 'manifest.json')
Compress-Archive -LiteralPath $taskOutput -DestinationPath ($taskOutput+'.zip')
Write-Host "Package: $taskOutput"
Write-Host "Archive: $taskOutput.zip"
