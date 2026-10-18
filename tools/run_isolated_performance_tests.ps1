[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $StrictAllocationOnly,
    [string] $ResultsDirectory = 'artifacts/test-results/strict-allocation'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$performanceDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot 'Game.Tests/PerformanceTests'))

Push-Location $repositoryRoot
try
{
    if ($StrictAllocationOnly)
    {
        # Keep strict zero-byte assertions and their warmups unchanged. Discover
        # every tagged case, then measure each in its own fresh test process.
        $discovered = & dotnet test Game.Tests/Game.Tests.csproj `
            --configuration $Configuration --no-build --no-restore --nologo `
            --list-tests --filter 'Category=StrictAllocation'
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $testNames = @($discovered | ForEach-Object { $_.Trim() } |
            Where-Object { $_.StartsWith('Game.Tests.', [StringComparison]::Ordinal) } |
            Sort-Object -Unique)
        if ($testNames.Count -eq 0) { throw 'No strict allocation tests discovered.' }
        $runRoot = Join-Path ([System.IO.Path]::GetFullPath($ResultsDirectory)) ([Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
        $passedCases = 0
        foreach ($testName in $testNames)
        {
            if ($testName -notmatch '^Game\.Tests\.[A-Za-z0-9_.]+$')
            {
                throw "Strict allocation discovery requires a fully qualified Fact name: $testName"
            }
            Write-Host "STRICT_ALLOCATION_CASE $testName"
            $trxName = "allocation-$passedCases.trx"
            & dotnet test Game.Tests/Game.Tests.csproj `
                --configuration $Configuration --no-build --no-restore --nologo `
                --filter "FullyQualifiedName=$testName" `
                --logger "trx;LogFileName=$trxName" --results-directory $runRoot
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
            [xml] $report = Get-Content -LiteralPath (Join-Path $runRoot $trxName) -Raw
            $counters = $report.TestRun.ResultSummary.Counters
            if ([int]$counters.total -ne 1 -or [int]$counters.executed -ne 1 -or
                [int]$counters.passed -ne 1 -or [int]$counters.failed -ne 0)
            {
                throw "Expected exactly one executed, passing allocation contract: $testName"
            }
            $passedCases++
        }
        Write-Host "STRICT_ALLOCATION_CASES_PASSED=$passedCases"
        return
    }

    $paths = @()
    $paths += Get-ChildItem -LiteralPath $performanceDirectory -Filter '*Tests.cs' -File
    $paths += Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'Game.Tests') `
        -Recurse `
        -Filter '*PerformanceTests.cs' `
        -File |
        Where-Object { [System.IO.Path]::GetFullPath($_.DirectoryName) -ne $performanceDirectory }
    $paths = $paths | Sort-Object FullName -Unique

    $passedClasses = 0
    foreach ($path in $paths)
    {
        $namespaceMatch = Select-String `
            -LiteralPath $path.FullName `
            -Pattern '^namespace\s+([^;]+);' |
            Select-Object -First 1
        if ($null -eq $namespaceMatch)
        {
            throw "No file-scoped namespace found in '$($path.FullName)'."
        }

        $namespace = $namespaceMatch.Matches[0].Groups[1].Value
        $fullyQualifiedClassName = "$namespace.$($path.BaseName)"
        Write-Host "PERF_CLASS $fullyQualifiedClassName"

        & dotnet test Game.Tests/Game.Tests.csproj `
            --configuration $Configuration `
            --no-build `
            --no-restore `
            --nologo `
            --filter "FullyQualifiedName~$fullyQualifiedClassName" `
            --logger 'console;verbosity=minimal'
        if ($LASTEXITCODE -ne 0)
        {
            exit $LASTEXITCODE
        }

        $passedClasses++
    }

    Write-Host "PERF_CLASSES_PASSED=$passedClasses"
}
finally
{
    Pop-Location
}
