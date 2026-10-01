<#
.SYNOPSIS
  直接调用 MCP for Unity 的 Streamable HTTP 端点，用于在 DSH 原生桥接
  (mcp__unity__*) 尚未装载时手工调用 Unity 工具。

.EXAMPLE
  pwsh Tools/unity-mcp-call.ps1 -List
  pwsh Tools/unity-mcp-call.ps1 -Tool read_console -Arguments '{"action":"get","types":["error"],"count":5}'
  pwsh Tools/unity-mcp-call.ps1 -Tool manage_scene -Arguments '{"action":"get_active"}'
#>
[CmdletBinding()]
param(
  [string]$Url = 'http://127.0.0.1:8080/mcp',
  [string]$Tool,
  [string]$Arguments = '{}',
  [switch]$List,
  [int]$TimeoutSec = 180
)

$ErrorActionPreference = 'Stop'
$headers = @{ Accept = 'application/json, text/event-stream' }

function Read-SsePayload([string]$Content) {
  $joined = ($Content -split "`r?`n" | Where-Object { $_ -like 'data:*' }) -join ''
  if (-not $joined) { return $null }
  return ($joined.Substring(5)).Trim() | ConvertFrom-Json
}

# 1. initialize -> capture the session id the server hands out
$init = @{
  jsonrpc = '2.0'; id = 1; method = 'initialize'
  params  = @{
    protocolVersion = '2024-11-05'; capabilities = @{}
    clientInfo      = @{ name = 'dsh-unity-mcp-call'; version = '1.0' }
  }
} | ConvertTo-Json -Depth 8 -Compress

$r = Invoke-WebRequest -Uri $Url -Method Post -Body $init -ContentType 'application/json' -Headers $headers -TimeoutSec $TimeoutSec -UseBasicParsing
$session = $r.Headers['mcp-session-id']
if (-not $session) { throw 'server did not return an mcp-session-id header' }
$headers['mcp-session-id'] = $session

# 2. required initialized notification
$null = Invoke-WebRequest -Uri $Url -Method Post -ContentType 'application/json' -Headers $headers `
  -Body '{"jsonrpc":"2.0","method":"notifications/initialized"}' -TimeoutSec $TimeoutSec -UseBasicParsing

if ($List) {
  $payload = Read-SsePayload (Invoke-WebRequest -Uri $Url -Method Post -ContentType 'application/json' -Headers $headers `
      -Body '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' -TimeoutSec $TimeoutSec -UseBasicParsing).Content
  $payload.result.tools | Sort-Object name | ForEach-Object { $_.name }
  return
}

if (-not $Tool) { throw 'pass -Tool <mcp tool name> (or -List)' }

# 3. tools/call with the raw MCP tool name
$call = @{ jsonrpc = '2.0'; id = 3; method = 'tools/call'; params = @{ name = $Tool; arguments = ($Arguments | ConvertFrom-Json) } } |
  ConvertTo-Json -Depth 12 -Compress
$response = Read-SsePayload (Invoke-WebRequest -Uri $Url -Method Post -ContentType 'application/json' -Headers $headers `
    -Body $call -TimeoutSec $TimeoutSec -UseBasicParsing).Content

if ($response.error) { Write-Error ("MCP error: " + ($response.error | ConvertTo-Json -Depth 6 -Compress)); exit 1 }

foreach ($block in $response.result.content) {
  if ($block.type -eq 'text') { $block.text } else { "[$($block.type) block]" }
}
if ($response.result.isError) { exit 1 }
