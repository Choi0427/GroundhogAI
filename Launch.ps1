[CmdletBinding()]
param(
    [int]$Seed = 1,
    [string]$AIConfig = (Join-Path $PSScriptRoot 'Configs\ai-default.json')
)
$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot 'Build\Windows\GroundhogAI.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw "Build is missing: $executable" }
$config = (Resolve-Path -LiteralPath $AIConfig).Path
& $executable '--seed' $Seed '--ai-config' $config
