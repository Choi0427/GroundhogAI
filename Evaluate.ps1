[CmdletBinding()]
param(
    [ValidateRange(1, 1000000)][int]$Episodes = 20,
    [int]$Seed = 1,
    [ValidateRange(0.016, 86400)][double]$MaxSeconds = 300,
    [string]$AIConfig = (Join-Path $PSScriptRoot 'Configs\ai-default.json'),
    [string]$OutputPath = (Join-Path $PSScriptRoot ('Evaluation\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))),
    [string]$Executable = (Join-Path $PSScriptRoot 'Build\Windows\GroundhogAI.exe')
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
    throw "Windows build is not present. Open this project in Unity 6000.5.5f1 and build it first, or run Build.ps1 when you want a build. Expected: $Executable"
}
$executablePath = (Resolve-Path -LiteralPath $Executable).Path
$configPath = (Resolve-Path -LiteralPath $AIConfig).Path
$resultDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
if ([long]$Seed + $Episodes - 1 -gt [int]::MaxValue) { throw 'The requested seed sequence exceeds Int32.MaxValue.' }
foreach ($report in @('summary.json', 'episodes.json', 'episodes.csv', 'ai-config.json')) {
    if (Test-Path -LiteralPath (Join-Path $resultDirectory $report)) {
        throw "A report already exists in '$resultDirectory'. Set -OutputPath to a new directory."
    }
}
[IO.Directory]::CreateDirectory($resultDirectory) | Out-Null
$logPath = Join-Path $resultDirectory 'player.log'
$invariantCulture = [Globalization.CultureInfo]::InvariantCulture

# Windows PowerShell 5.1 ProcessStartInfo has no ArgumentList property.
# Quote each Windows argv item, including backslashes before a closing quote.
function ConvertTo-WindowsArgument([string]$Value) {
    if ($Value -notmatch '[\s"]' -and $Value.Length -gt 0) { return $Value }
    return '"' + [regex]::Replace([regex]::Replace($Value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}

$playerArguments = @(
    '-batchmode', '-nographics', '-logFile', $logPath,
    '--evaluate', '--episodes', $Episodes.ToString($invariantCulture),
    '--seed', $Seed.ToString($invariantCulture),
    '--max-seconds', $MaxSeconds.ToString('R', $invariantCulture),
    '--ai-config', $configPath, '--results', $resultDirectory
)
$startInfo = New-Object Diagnostics.ProcessStartInfo
$startInfo.FileName = $executablePath
$startInfo.WorkingDirectory = Split-Path -Parent $executablePath
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.Arguments = ($playerArguments | ForEach-Object { ConvertTo-WindowsArgument $_ }) -join ' '
Write-Host "Evaluating $Episodes episode(s), seed $Seed, limit $MaxSeconds seconds..."
$player = [Diagnostics.Process]::Start($startInfo)
$player.WaitForExit()
if ($player.ExitCode -ne 0) { throw "Evaluation exited with code $($player.ExitCode). See $logPath" }
$summaryPath = Join-Path $resultDirectory 'summary.json'
if (-not (Test-Path -LiteralPath $summaryPath)) { throw "Player exited without producing summary.json. See $logPath" }
$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
Write-Host ('Mean: {0:F3}s  Median: {1:F3}s  Longest: {2:F3}s' -f $summary.meanSurvivalSeconds, $summary.medianSurvivalSeconds, $summary.longestSurvivalSeconds)
Write-Host ('Episodes: {0}  Stage completed: {1}  Time-limit episodes: {2}  AI: {3}  Rules: {4}' -f $summary.episodes, $summary.stageCompletedEpisodes, $summary.timeLimitEpisodes, $summary.configVersion, $summary.rulesVersion)
Write-Host "Results: $resultDirectory"
