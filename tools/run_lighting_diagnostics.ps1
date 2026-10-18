[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PublishRoot,
    [string] $OutputDirectory = 'artifacts/lighting-diagnostics',
    [ValidateRange(640, 2560)] [int] $Width = 1280,
    [ValidateRange(360, 1440)] [int] $Height = 720,
    [ValidateRange(120, 4096)] [int] $Frames = 480,
    [ValidateRange(1, 2000)] [int] $WarmupFrames = 120,
    [ValidateRange(2, 5)] [int] $Repeats = 2
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Frames - $WarmupFrames -lt 60) { throw 'Need at least 60 measured frames.' }
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'client_capture_environment.ps1')
Initialize-ClientCaptureEnvironment -RepositoryRoot $repositoryRoot
$runRoot = Join-Path ([System.IO.Path]::GetFullPath($OutputDirectory)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$scenarios = @(
    @('forest_day', 'forest_day', 'Static', 0, $false),
    @('forest_night', 'forest_night', 'Static', 2, $false),
    @('cave_lit', 'cave_lit', 'Static', 2, $false),
    @('forest_pan', 'forest_night', 'Pan', 2, $false),
    @('forest_zoom', 'forest_night', 'Zoom', 2, $false),
    @('cave_panzoom', 'cave_lit', 'PanZoom', 2, $false),
    @('dense_lights', 'forest_night', 'Static', 12, $false),
    @('night_no_bloom', 'forest_night', 'Static', 2, $true),
    @('core_dirty', 'forest_day', 'Static', 0, $false)
)
$results = @()
$failures = @()
foreach ($scenario in $scenarios) {
    $name, $source, $cameraPath, $lightCount, $disableBloom = $scenario
    $sceneRoot = Join-Path $runRoot $name
    New-Item -ItemType Directory -Path $sceneRoot -Force | Out-Null
    $sourcePath = Join-Path $PSScriptRoot "golden-scenes/$source.json"
    $fixture = Get-Content -LiteralPath $sourcePath -Raw | ConvertFrom-Json -AsHashtable
    $fixture.SceneId = "diag_$name"
    $fixture.Width = $Width; $fixture.Height = $Height
    $fixture.Frames = $Frames; $fixture.WarmupFrames = $WarmupFrames
    $fixture.Diagnostic = @{ CameraPath = $cameraPath; DynamicLightCount = $lightCount; DisableBloom = $disableBloom }
    if ($name -eq 'core_dirty') { $fixture.Diagnostic.ForceCoreLightingInterval = 60 }
    $fixturePath = Join-Path $sceneRoot fixture.json
    $fixture | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $fixturePath -Encoding utf8NoBOM
    try {
        for ($sample = 0; $sample -lt $Repeats; $sample++) {
            $sampleRoot = Join-Path $sceneRoot "run-$sample"
            & (Join-Path $PSScriptRoot 'run_client_smokes.ps1') -PublishRoot $PublishRoot -OutputDirectory $sampleRoot -GoldenFixture $fixturePath -TimeoutSeconds 180
            $captures = @(Get-ChildItem -LiteralPath $sampleRoot -Recurse -Filter 'client-smoke-playing.json' -File)
            if ($captures.Count -ne 1) { throw "Expected one capture for $name/$sample." }
            $capture = $captures[0].FullName
            & python (Join-Path $PSScriptRoot 'lighting_diagnostic_report.py') --capture $capture --output-dir (Join-Path $sampleRoot report)
            if ($LASTEXITCODE -ne 0) { throw "Invalid diagnostic measurements for $name/$sample." }
            if ($sample -eq 0) { $firstCapture = $capture }
            else {
                & python (Join-Path $PSScriptRoot 'golden_scene_compare.py') --reference ([System.IO.Path]::ChangeExtension($firstCapture, '.png')) --candidate ([System.IO.Path]::ChangeExtension($capture, '.png')) --output-dir (Join-Path $sampleRoot repeat-diff) --mode exact *> (Join-Path $sampleRoot repeat-comparison.log)
                if ($LASTEXITCODE -ne 0) { throw "Nonreproducible diagnostic capture: $name/$sample." }
            }
            $results += [ordered]@{ SceneId = $name; Repeat = $sample; Report = Join-Path $sampleRoot 'report/report.json'; SourceFixtureSHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash }
        }
        Write-Host "LIGHTING_DIAGNOSTIC_PASS scene=$name repeats=$Repeats measured=$($Frames - $WarmupFrames) size=${Width}x${Height}"
    } catch { $failures += "$name`: $($_.Exception.Message)" }
}
[ordered]@{
    Version = 1; Results = $results; Failures = $failures
    Policy = 'Unpaced fixed-60Hz simulation workload, not real-time gameplay FPS; VSync and software cap off. CPU API time includes unknown driver waits; GPU execution unknown. Original Golden references untouched.'
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $runRoot summary.json) -Encoding utf8NoBOM
Write-Host "LIGHTING_DIAGNOSTIC_RESULTS=$runRoot"
if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
