param(
    [string]$Summary,
    [string]$ValidationLabel,
    [switch]$Check
)

$ErrorActionPreference = "Stop"
$websiteRoot = Split-Path -Parent $PSScriptRoot
$statusPath = Join-Path $websiteRoot "static/data/status.json"

if (-not $Check) {
    $status = Get-Content -Raw -Encoding utf8 -LiteralPath $statusPath | ConvertFrom-Json
    $status.updated = (Get-Date).ToString("yyyy-MM-dd")
    if ($Summary) { $status.summary = $Summary }
    if ($ValidationLabel) {
        $validationMetric = $status.metrics | Where-Object {
            $_.label -in @("Validation", "Debug + Release tests", "Debug + Release suites", "Latest recorded Release suite")
        } | Select-Object -First 1
        if ($validationMetric) { $validationMetric.value = $ValidationLabel }
    }
    $json = $status | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText(
        $statusPath,
        $json + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
}

Push-Location $websiteRoot
try {
    npm run build
    if ($LASTEXITCODE -ne 0) {
        throw "Website build failed with exit code $LASTEXITCODE."
    }
    node (Join-Path $PSScriptRoot "validate-site.mjs")
} finally {
    Pop-Location
}
if ($LASTEXITCODE -ne 0) {
    throw "Website validation failed with exit code $LASTEXITCODE."
}

if ($Check) {
    Write-Output "Website check completed without modifying data."
} else {
    Write-Output "Website data updated and validated."
}
