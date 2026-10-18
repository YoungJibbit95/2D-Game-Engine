[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PublishRoot,
    [string] $OutputDirectory = 'artifacts/lighting-diagnostics',
    [ValidateRange(640, 2560)] [int] $Width = 1280,
    [ValidateRange(360, 1440)] [int] $Height = 720,
    [ValidateRange(120, 4096)] [int] $Frames = 480,
    [ValidateRange(1, 2000)] [int] $WarmupFrames = 120,
    [ValidateRange(2, 5)] [int] $Repeats = 2,
    [ValidateSet('inherit', 'on', 'off')] [string] $PreparationReuse = 'inherit',
    [switch] $CompareReuseFallback,
    [switch] $IncludeReuseLifecycle
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Assert-ReuseMode([string] $CapturePath, [string] $ExpectedMode) {
    $capture = Get-Content -LiteralPath $CapturePath -Raw | ConvertFrom-Json -Depth 100
    if ($capture.BenchmarkContext.LightingPreparationReuse -cne $ExpectedMode) { throw "Client did not attest preparation reuse $ExpectedMode." }
    $frames = @($capture.LightingDiagnostics.Frames)
    $prepares = ($frames | Measure-Object -Property PrepareExecutions -Sum).Sum
    $lookups = ($frames | Measure-Object -Property CacheLookups -Sum).Sum
    $bypasses = ($frames | Measure-Object -Property CacheBypasses -Sum).Sum
    if (($ExpectedMode -eq 'on' -and $lookups -ne $prepares) -or
        ($ExpectedMode -eq 'off' -and ($lookups -ne 0 -or $bypasses -ne $prepares))) {
        throw 'Client cache work does not match the requested mode for this bounded fixture.'
    }
}
function Assert-ReuseLifecycle([string] $CapturePath) {
    $capture = Get-Content -LiteralPath $CapturePath -Raw | ConvertFrom-Json -Depth 100
    $fixture = $capture.GoldenScene.Fixture
    $frames = @($capture.LightingDiagnostics.Frames)
    $expectedResets = [Math]::Floor($fixture.Frames / $fixture.Diagnostic.ForceDeviceResetInterval) -
        [Math]::Floor($fixture.WarmupFrames / $fixture.Diagnostic.ForceDeviceResetInterval)
    $rebuilds = ($frames | Measure-Object -Property TextureRebuilds -Sum).Sum
    if ($rebuilds -lt $expectedResets -or $expectedResets -lt 1) { throw 'Device reset did not recreate measured lighting resources.' }
    if ($capture.GoldenScene.TimeOfDay -eq $fixture.TimeOfDay) { throw 'Lifecycle clock did not advance.' }
    if (($frames | Measure-Object -Property PointLightsUsed -Maximum).Maximum -eq 0 -or $frames[-1].PointLightsUsed -ne 0) {
        throw 'Lifecycle source removal was not observed.'
    }
    if ($capture.BenchmarkContext.LightingPreparationReuse -eq 'on' -and
        ($frames | Measure-Object -Property PortalCacheHits -Sum).Sum -lt 1) { throw 'Lifecycle never reused the checked prefix.' }
}
if ($Frames - $WarmupFrames -lt 60) { throw 'Need at least 60 measured frames.' }
if ($IncludeReuseLifecycle -and ($Frames -lt 480 -or $WarmupFrames -gt $Frames / 2 - 60)) {
    throw 'Lifecycle fixture needs >=480 frames and a warmup ending at least 60 ticks before source removal.'
}
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
if ($IncludeReuseLifecycle) { $scenarios += ,@('reuse_lifecycle', 'forest_night', 'Static', 2, $false) }
$results = @()
$fallbackResults = @()
$failures = @()
$actualMode = if ($PreparationReuse -eq 'inherit') {
    if ($env:YJSE_DISABLE_LIGHTING_PREPARATION_REUSE -eq '1') { 'off' } else { 'on' }
} else { $PreparationReuse }
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
    if ($name -eq 'reuse_lifecycle') {
        $fixture.Diagnostic.AdvanceWorldTime = $true
        $fixture.Diagnostic.ForceDeviceResetInterval = 113
        $fixture.Diagnostic.DynamicLightLifetimeTicks = [int][Math]::Floor($Frames / 2)
    }
    $fixturePath = Join-Path $sceneRoot fixture.json
    $fixture | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $fixturePath -Encoding utf8NoBOM
    try {
        for ($sample = 0; $sample -lt $Repeats; $sample++) {
            $sampleRoot = Join-Path $sceneRoot "run-$sample"
            & (Join-Path $PSScriptRoot 'run_client_smokes.ps1') -PublishRoot $PublishRoot -OutputDirectory $sampleRoot -GoldenFixture $fixturePath -TimeoutSeconds 180 -PreparationReuse $PreparationReuse
            $captures = @(Get-ChildItem -LiteralPath $sampleRoot -Recurse -Filter 'client-smoke-playing.json' -File)
            if ($captures.Count -ne 1) { throw "Expected one capture for $name/$sample." }
            $capture = $captures[0].FullName
            & python (Join-Path $PSScriptRoot 'lighting_diagnostic_report.py') --capture $capture --output-dir (Join-Path $sampleRoot report)
            if ($LASTEXITCODE -ne 0) { throw "Invalid diagnostic measurements for $name/$sample." }
            if ($PreparationReuse -ne 'inherit' -or $CompareReuseFallback) { Assert-ReuseMode $capture $actualMode }
            if ($name -eq 'reuse_lifecycle') { Assert-ReuseLifecycle $capture }
            if ($sample -eq 0) { $firstCapture = $capture }
            else {
                & python (Join-Path $PSScriptRoot 'golden_scene_compare.py') --reference ([System.IO.Path]::ChangeExtension($firstCapture, '.png')) --candidate ([System.IO.Path]::ChangeExtension($capture, '.png')) --output-dir (Join-Path $sampleRoot repeat-diff) --mode exact *> (Join-Path $sampleRoot repeat-comparison.log)
                if ($LASTEXITCODE -ne 0) { throw "Nonreproducible diagnostic capture: $name/$sample." }
            }
            $results += [ordered]@{ SceneId = $name; Repeat = $sample; Report = Join-Path $sampleRoot 'report/report.json'; SourceFixtureSHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash }
        }
        if ($CompareReuseFallback) {
            $fallbackMode = if ($actualMode -eq 'on') { 'off' } else { 'on' }
            $fallbackRoot = Join-Path $sceneRoot "fallback-$fallbackMode"
            & (Join-Path $PSScriptRoot 'run_client_smokes.ps1') -PublishRoot $PublishRoot -OutputDirectory $fallbackRoot -GoldenFixture $fixturePath -TimeoutSeconds 180 -PreparationReuse $fallbackMode
            $fallbackCaptures = @(Get-ChildItem -LiteralPath $fallbackRoot -Recurse -Filter 'client-smoke-playing.json' -File)
            if ($fallbackCaptures.Count -ne 1) { throw "Expected one fallback capture for $name." }
            $fallbackCapture = $fallbackCaptures[0].FullName
            & python (Join-Path $PSScriptRoot 'lighting_diagnostic_report.py') --capture $fallbackCapture --output-dir (Join-Path $fallbackRoot report)
            if ($LASTEXITCODE -ne 0) { throw "Invalid fallback measurements for $name." }
            Assert-ReuseMode $fallbackCapture $fallbackMode
            if ($name -eq 'reuse_lifecycle') { Assert-ReuseLifecycle $fallbackCapture }
            & python (Join-Path $PSScriptRoot 'golden_scene_compare.py') --reference ([System.IO.Path]::ChangeExtension($firstCapture, '.png')) --candidate ([System.IO.Path]::ChangeExtension($fallbackCapture, '.png')) --output-dir (Join-Path $fallbackRoot exact-diff) --mode exact *> (Join-Path $fallbackRoot comparison.log)
            if ($LASTEXITCODE -ne 0) { throw "Preparation reuse changes pixels/state: $name." }
            $fallbackResults += [ordered]@{ SceneId = $name; Mode = $fallbackMode; Report = Join-Path $fallbackRoot 'report/report.json'; Comparison = Join-Path $fallbackRoot 'exact-diff/comparison.json' }
        }
        Write-Host "LIGHTING_DIAGNOSTIC_PASS scene=$name repeats=$Repeats measured=$($Frames - $WarmupFrames) size=${Width}x${Height}"
    } catch { $failures += "$name`: $($_.Exception.Message)" }
}
[ordered]@{
    Version = 1; Results = $results; Failures = $failures; PreparationReuse = $PreparationReuse; FallbackResults = $fallbackResults
    Policy = 'Unpaced fixed-60Hz simulation workload, not real-time gameplay FPS; VSync and software cap off. CPU API time includes unknown driver waits; GPU execution unknown. Original Golden references untouched.'
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $runRoot summary.json) -Encoding utf8NoBOM
Write-Host "LIGHTING_DIAGNOSTIC_RESULTS=$runRoot"
if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
