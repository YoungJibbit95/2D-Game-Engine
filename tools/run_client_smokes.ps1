[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PublishRoot,
    [Parameter(Mandatory)] [string] $OutputDirectory,
    [ValidateRange(1, 300)] [int] $TimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$publishPath = (Resolve-Path -LiteralPath $PublishRoot).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$clientPath = Join-Path $publishPath 'Game.Client.dll'
if (-not (Test-Path -LiteralPath $clientPath -PathType Leaf)) { throw "Missing published client: $clientPath" }
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

# A fresh directory prevents a previous run's captures from satisfying the gate.
$runPath = Join-Path $outputPath ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runPath | Out-Null
$failures = @()
foreach ($state in @('main-menu', 'world-select', 'playing')) {
    $capture = Join-Path $runPath "client-smoke-$state.png"
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
    $startInfo.WorkingDirectory = $publishPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Environment['YJSE_GAME_ROOT'] = $publishPath
    $startInfo.Environment.Remove('YJSE_GAME_DATA') | Out-Null
    $startInfo.Environment.Remove('YJSE_MODS_ROOT') | Out-Null
    foreach ($argument in @($clientPath, '--smoke', '--start-state', $state,
        '--frames', '30', '--warmup-frames', '10', '--frame-limit', '60',
        '--timeout-seconds', "$TimeoutSeconds", '--output', $capture)) {
        $startInfo.ArgumentList.Add($argument)
    }
    if ($state -eq 'playing') {
        $startInfo.ArgumentList.Add('--scene-biome')
        $startInfo.ArgumentList.Add('forest')
        $startInfo.ArgumentList.Add('--scene-time')
        $startInfo.ArgumentList.Add('day')
        $startInfo.ArgumentList.Add('--scene-weather')
        $startInfo.ArgumentList.Add('clear')
    }

    $clientProcess = [System.Diagnostics.Process]::Start($startInfo)
    $stdout = $clientProcess.StandardOutput.ReadToEndAsync()
    $stderr = $clientProcess.StandardError.ReadToEndAsync()
    try {
        # Covers native graphics initialization too, before the client's watchdog starts.
        if (-not $clientProcess.WaitForExit(($TimeoutSeconds + 10) * 1000)) {
            $clientProcess.Kill($true)
            $clientProcess.WaitForExit()
            throw "Client smoke '$state' exceeded its external deadline."
        }
        if ($clientProcess.ExitCode -ne 0) { throw "Client smoke '$state' exited with $($clientProcess.ExitCode)." }
        $report = Get-Content -LiteralPath ([System.IO.Path]::ChangeExtension($capture, '.json')) -Raw | ConvertFrom-Json
        if ($report.Passed -ne $true -or -not (Test-Path -LiteralPath $capture -PathType Leaf)) {
            throw "Client smoke '$state' did not produce a passing capture."
        }
        if ($state -eq 'playing' -and $report.Gameplay.Captured -ne $true) {
            throw 'Playing smoke did not reach the game world.'
        }
        Write-Host "SMOKE_PASS state=$state frame=$($report.CapturedFrame) scenePixels=$($report.NonBlackPixels) resources=$($report.TextureResourceCount) frames=$($report.TextureFrameCount) capture=$capture"
    } catch {
        $failures += $_.Exception.Message
    } finally {
        $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $runPath "client-smoke-$state.stdout.log")
        $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $runPath "client-smoke-$state.stderr.log")
        $clientProcess.Dispose()
    }
}
if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
