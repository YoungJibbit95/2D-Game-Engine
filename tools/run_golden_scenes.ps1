[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PublishRoot,
    [string] $OutputDirectory = 'artifacts/golden-scenes',
    [string] $BaselineRoot = 'tools/golden-scenes/baselines/reference',
    [ValidateSet('auto', 'exact', 'portable')] [string] $BaselineMode = 'auto',
    [switch] $CaptureOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publishPath = (Resolve-Path -LiteralPath $PublishRoot).Path
$runRoot = Join-Path ([System.IO.Path]::GetFullPath($OutputDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

. (Join-Path $PSScriptRoot 'client_capture_environment.ps1')
Initialize-ClientCaptureEnvironment -RepositoryRoot $repositoryRoot

$results = @()
$failedScenes = @()
foreach ($scene in @('forest_day', 'forest_night', 'cave_lit', 'world_select')) {
    $sceneRoot = Join-Path $runRoot $scene
    $fixture = Join-Path $PSScriptRoot "golden-scenes/$scene.json"
    try {
        foreach ($sample in @('reference', 'candidate')) {
            $sampleRoot = Join-Path $sceneRoot $sample
            & (Join-Path $PSScriptRoot 'run_client_smokes.ps1') -PublishRoot $publishPath -OutputDirectory $sampleRoot -GoldenFixture $fixture
            $images = @(Get-ChildItem -LiteralPath $sampleRoot -Recurse -File -Filter 'client-smoke-*.png')
            if ($images.Count -ne 1) { throw "Expected exactly one new capture for $scene/$sample." }
            Copy-Item -LiteralPath $images[0].FullName -Destination (Join-Path $sceneRoot "$sample.png")
            Copy-Item -LiteralPath ([System.IO.Path]::ChangeExtension($images[0].FullName, '.json')) -Destination (Join-Path $sceneRoot "$sample.json")
        }
        & python (Join-Path $PSScriptRoot 'golden_scene_compare.py') --reference (Join-Path $sceneRoot reference.png) --candidate (Join-Path $sceneRoot candidate.png) --output-dir (Join-Path $sceneRoot exact) --mode exact
        if ($LASTEXITCODE -ne 0) { throw "Repeated fresh-process capture differs for $scene." }
        if (-not $CaptureOnly) {
            $comparisonMode = $BaselineMode
            if ($comparisonMode -eq 'auto') {
                $baselineMetadata = Get-Content -LiteralPath (Join-Path $BaselineRoot "$scene.json") -Raw | ConvertFrom-Json
                $candidateMetadata = Get-Content -LiteralPath (Join-Path $sceneRoot candidate.json) -Raw | ConvertFrom-Json
                $baselineEnvironment = ($baselineMetadata.CaptureEnvironment.PSObject.Properties | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join "`n"
                $candidateEnvironment = ($candidateMetadata.CaptureEnvironment.PSObject.Properties | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join "`n"
                $comparisonMode = if ($baselineEnvironment -ceq $candidateEnvironment) { 'exact' } else { 'portable' }
            }
            & python (Join-Path $PSScriptRoot 'golden_scene_compare.py') --reference (Join-Path $BaselineRoot "$scene.png") --candidate (Join-Path $sceneRoot candidate.png) --output-dir (Join-Path $sceneRoot baseline) --mode $comparisonMode
            if ($LASTEXITCODE -ne 0) { throw "Versioned baseline differs for $scene." }
        }
        $capture = Get-Content -LiteralPath (Join-Path $sceneRoot candidate.json) -Raw | ConvertFrom-Json
        $results += [ordered]@{
            SceneId = $scene; GoldenScene = $capture.GoldenScene; CaptureEnvironment = $capture.CaptureEnvironment
            BenchmarkContext = $capture.BenchmarkContext; FrameTiming = $capture.FrameTiming
            PerformanceMetrics = $capture.PerformanceMetrics; ComparisonRoot = $sceneRoot
        }
        Write-Host "GOLDEN_PASS scene=$scene exact=fresh-process baseline=$(if ($CaptureOnly) { 'not-run-capture-only' } else { $comparisonMode })"
    } catch {
        $failedScenes += "$scene`: $($_.Exception.Message)"
    }
}
[ordered]@{
    Version = 1; CaptureOnly = [bool]$CaptureOnly; Results = $results; Failures = $failedScenes
    TimingPolicy = 'Real CPU Stopwatch and frame intervals after 60 warmup frames; 120 samples; 60 FPS cap. GPU time unknown. Profile averages are EMA.'
} | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $runRoot summary.json) -Encoding utf8NoBOM
if ($failedScenes.Count -gt 0) { throw ($failedScenes -join [Environment]::NewLine) }
Write-Host "GOLDEN_RESULTS=$runRoot"
