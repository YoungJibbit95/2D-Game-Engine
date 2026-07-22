[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$performanceDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot 'Game.Tests/PerformanceTests'))

Push-Location $repositoryRoot
try
{
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
