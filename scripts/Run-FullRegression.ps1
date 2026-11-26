[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "High")]
param()

$ErrorActionPreference = "Stop"

$startedAt = Get-Date
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss_fff"

$repositoryRoot = Split-Path -Parent $PSScriptRoot

$solution = Join-Path $repositoryRoot "FitnessPlatform.slnx"

$unitProject = Join-Path $repositoryRoot "tests\FitnessPlatform.UnitTests\FitnessPlatform.UnitTests.csproj"

$integrationProject = Join-Path $repositoryRoot "tests\FitnessPlatform.IntegrationTests\FitnessPlatform.IntegrationTests.csproj"

$statisticsScript = Join-Path $PSScriptRoot "Get-IntegrationTestDatabaseStatistics.ps1"

$resetScript = Join-Path $PSScriptRoot "Reset-IntegrationTestDatabase.ps1"

$resultsRoot = Join-Path $repositoryRoot "TestResults\AutomatedRegression"

$resultsDirectory = Join-Path $resultsRoot $timestamp

$logFile = Join-Path $resultsDirectory "RegressionExecution.log"

$summaryFile = Join-Path $resultsDirectory "RegressionSummary.txt"

$unitTrx = Join-Path $resultsDirectory "UnitTests.trx"

$integrationTrx = Join-Path $resultsDirectory "IntegrationTests.trx"

$exitCode = 0
$transcriptStarted = $false
$locationPushed = $false
$regressionStatus = "NOT STARTED"
$mutex = $null
$mutexAcquired = $false
$mutexName = "Global\FitnessPlatformIntegrationTestsDbRegression"

function Write-Section {

    param([string]$Title)

    Write-Host ""
    Write-Host "==================================================" -ForegroundColor Cyan
    Write-Host " $Title" -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Cyan
    Write-Host ""

}

function Invoke-DotNet {

    param(
        [string[]]$Arguments
    )

    Write-Host "dotnet $($Arguments -join ' ')" -ForegroundColor Yellow

    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE."
    }

}

function Invoke-DatabaseStatistics {

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $statisticsScript

    if ($LASTEXITCODE -ne 0) {
        throw "Database statistics execution failed."
    }

}

function Get-TestSummary {

    param(
        [string]$ReportPath,
        [string]$ProjectName
    )

    if (-not (Test-Path $ReportPath)) {

        return [PSCustomObject]@{
            Project  = $ProjectName
            Total    = 0
            Executed = 0
            Passed   = 0
            Failed   = 0
            Skipped  = 0
            Status   = "REPORT NOT FOUND"
        }

    }

    [xml]$trx = Get-Content $ReportPath -Raw

    $counters = $trx.SelectSingleNode(
        "//*[local-name()='Counters']"
    )

    if ($null -eq $counters) {
        throw "Invalid TRX report: $ReportPath"
    }

    $total = [int]$counters.GetAttribute("total")
    $executed = [int]$counters.GetAttribute("executed")
    $passed = [int]$counters.GetAttribute("passed")
    $failed = [int]$counters.GetAttribute("failed")
    $skipped = [int]$counters.GetAttribute("notExecuted")

    $outcomeNode = $trx.SelectSingleNode(
        "//*[local-name()='ResultSummary']"
    )

    $outcome = $outcomeNode.GetAttribute("outcome")

    return [PSCustomObject]@{
        Project  = $ProjectName
        Total    = $total
        Executed = $executed
        Passed   = $passed
        Failed   = $failed
        Skipped  = $skipped
        Status   = $outcome
    }

}

try {

    Write-Section "FITNESSPLATFORM FULL REGRESSION"

    Write-Host "Repository : $repositoryRoot"
    Write-Host "Started    : $startedAt"

    $requiredFiles = @(
        $solution,
        $unitProject,
        $integrationProject,
        $statisticsScript,
        $resetScript
    )

    foreach ($file in $requiredFiles) {

        if (-not (Test-Path $file)) {
            throw "Required file not found: $file"
        }

    }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet CLI is not available."
    }

    if (-not $PSCmdlet.ShouldProcess(
        "FitnessPlatformIntegrationTestsDb",
        "Reset integration test data and execute full regression"
    )) {
        return
    }

    $mutex = [System.Threading.Mutex]::new($false, $mutexName)
    try {
        $mutexAcquired = $mutex.WaitOne(0)
    }
    catch [System.Threading.AbandonedMutexException] {
        $mutexAcquired = $true
        Write-Warning "An abandoned regression mutex was recovered."
    }
    if (-not $mutexAcquired) {
        throw "Another FitnessPlatform automated regression is already running. No database changes were made."
    }
    Write-Host "Regression execution lock acquired." -ForegroundColor Green

    New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

    Start-Transcript -Path $logFile | Out-Null
    $transcriptStarted = $true

    Push-Location $repositoryRoot
    $locationPushed = $true

    $regressionStatus = "RUNNING"

    Write-Section "STEP 1 - DATABASE STATISTICS BEFORE CLEANUP"

    Invoke-DatabaseStatistics

    Write-Section "STEP 2 - RESET INTEGRATION DATABASE"

    & $resetScript -Execute -Confirm:$false

    Write-Section "STEP 3 - DATABASE STATISTICS AFTER CLEANUP"

    Invoke-DatabaseStatistics

    Write-Section "STEP 4 - RESTORE NUGET PACKAGES"

    Invoke-DotNet -Arguments @(
        "restore",
        $solution
    )

    Write-Section "STEP 5 - BUILD SOLUTION"

    Invoke-DotNet -Arguments @(
        "build",
        $solution,
        "--configuration",
        "Debug",
        "--no-restore"
    )

    Write-Section "STEP 6 - EXECUTE ALL UNIT TESTS"

    Invoke-DotNet -Arguments @(
        "test",
        $unitProject,
        "--configuration",
        "Debug",
        "--no-build",
        "--no-restore",
        "--logger",
        "trx;LogFileName=UnitTests.trx",
        "--results-directory",
        $resultsDirectory
    )

    $unitResult = Get-TestSummary -ReportPath $unitTrx -ProjectName "Unit Tests"

    if ($unitResult.Total -le 0 -or
        $unitResult.Executed -ne $unitResult.Total -or
        $unitResult.Passed -ne $unitResult.Total -or
        $unitResult.Skipped -ne 0 -or
        $unitResult.Failed -gt 0 -or
        $unitResult.Status -ne "Completed") {

        throw "Unit test report validation failed."

    }

    Write-Section "STEP 7 - EXECUTE ALL INTEGRATION TESTS"

    Invoke-DotNet -Arguments @(
        "test",
        $integrationProject,
        "--configuration",
        "Debug",
        "--no-build",
        "--no-restore",
        "--logger",
        "trx;LogFileName=IntegrationTests.trx",
        "--results-directory",
        $resultsDirectory
    )

    $integrationResult = Get-TestSummary -ReportPath $integrationTrx -ProjectName "Integration Tests"

    if ($integrationResult.Total -le 0 -or
        $integrationResult.Executed -ne $integrationResult.Total -or
        $integrationResult.Passed -ne $integrationResult.Total -or
        $integrationResult.Skipped -ne 0 -or
        $integrationResult.Failed -gt 0 -or
        $integrationResult.Status -ne "Completed") {

        throw "Integration test report validation failed."

    }

    $regressionStatus = "PASSED"

}
catch {

    $exitCode = 1
    $regressionStatus = "FAILED"

    Write-Host ""
    Write-Host "REGRESSION FAILED" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red

}
finally {
    try {
    if ($transcriptStarted) {

        Write-Section "STEP 8 - FINAL DATABASE STATISTICS"

        try {

            Invoke-DatabaseStatistics

        }
        catch {

            $exitCode = 1
            $regressionStatus = "FAILED"

            Write-Warning $_.Exception.Message

        }

        Write-Section "STEP 9 - CONSOLIDATED REGRESSION SUMMARY"

        try {

            $summary = @(
                Get-TestSummary -ReportPath $unitTrx -ProjectName "Unit Tests"
                Get-TestSummary -ReportPath $integrationTrx -ProjectName "Integration Tests"
            )

            $summary | Format-Table -AutoSize

            $finishedAt = Get-Date
            $duration = $finishedAt - $startedAt

            $totalTests = ($summary | Measure-Object -Property Total -Sum).Sum
            $totalPassed = ($summary | Measure-Object -Property Passed -Sum).Sum
            $totalFailed = ($summary | Measure-Object -Property Failed -Sum).Sum
            $totalSkipped = ($summary | Measure-Object -Property Skipped -Sum).Sum

            $report = @"

FITNESSPLATFORM FULL REGRESSION SUMMARY
=======================================

Repository       : $repositoryRoot
Started          : $startedAt
Finished         : $finishedAt
Duration         : $duration
Total Tests      : $totalTests
Passed           : $totalPassed
Failed           : $totalFailed
Skipped          : $totalSkipped
Regression Status: $regressionStatus

$($summary | Format-Table -AutoSize | Out-String)

Results Directory:
$resultsDirectory

"@

            $report | Set-Content -Path $summaryFile -Encoding UTF8

            Write-Host $report

        }
        catch {

            $exitCode = 1
            $regressionStatus = "FAILED"

            Write-Warning $_.Exception.Message

        }
        finally {

            Stop-Transcript | Out-Null

        }

    }

    if ($locationPushed) {
        Pop-Location
    }

    }
    finally {
        try {
            if ($mutexAcquired -and $null -ne $mutex) {
                $mutex.ReleaseMutex()
                Write-Host "Regression execution lock released." -ForegroundColor Green
            }
        }
        finally {
            if ($null -ne $mutex) {
                $mutex.Dispose()
            }
        }
    }
}

exit $exitCode
