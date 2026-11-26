[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "High")]
param(
    [switch]$Execute
)

$ErrorActionPreference = "Stop"

$server = "(localdb)\MSSQLLocalDB"
$database = "FitnessPlatformIntegrationTestsDb"

$connectionString = "Server=$server;Database=$database;Integrated Security=True;TrustServerCertificate=True;"

$tablesToReset = @(
    "WorkoutSessionSets",
    "WorkoutSessionExercises",
    "WorkoutSessions",
    "MemberWorkoutPlanAssignments",
    "WorkoutPlanExercises",
    "WorkoutPlanDays",
    "WorkoutPlans",
    "ExerciseReviewAudits",
    "Exercises",
    "UserProfileFitnessGoals",
    "DietaryPreferences",
    "WorkoutPreferences",
    "UserProfiles",
    "FitnessGoals",
    "AspNetUserClaims",
    "AspNetUserLogins",
    "AspNetUserTokens",
    "AspNetUserRoles",
    "AspNetRoleClaims",
    "AspNetUsers"
)

$tablesToPreserve = @(
    "AspNetRoles",
    "__EFMigrationsHistory"
)

$expectedTables = @(
    $tablesToReset + $tablesToPreserve
)

$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = $connectionString

$transaction = $null

function Invoke-ScalarQuery {

    param(
        [string]$Query
    )

    $cmd = $conn.CreateCommand()

    try {

        $cmd.CommandText = $Query
        $cmd.CommandTimeout = 120

        if ($null -ne $transaction) {
            $cmd.Transaction = $transaction
        }

        return $cmd.ExecuteScalar()

    }
    finally {

        $cmd.Dispose()

    }
}

function Invoke-NonQuery {

    param(
        [string]$Query
    )

    $cmd = $conn.CreateCommand()

    try {

        $cmd.CommandText = $Query
        $cmd.CommandTimeout = 120
        $cmd.Transaction = $transaction

        return $cmd.ExecuteNonQuery()

    }
    finally {

        $cmd.Dispose()

    }
}

try {

    Write-Host ""
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host " FITNESSPLATFORM INTEGRATION DATABASE RESET " -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor Cyan

    $conn.Open()

    if ($conn.Database -ne $database) {
        throw "Unsafe database name detected."
    }

    if ($conn.DataSource -ne $server) {
        throw "Unsafe database server detected."
    }

    $actualDatabase = Invoke-ScalarQuery -Query "SELECT DB_NAME();"

    if ($actualDatabase -ne $database) {
        throw "Connected database validation failed."
    }

    Write-Host ""
    Write-Host "Server   : $server"
    Write-Host "Database : $database"
    Write-Host "Date     : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

    Write-Host ""
    Write-Host "Validating database schema..." -ForegroundColor Yellow

    $schemaQuery = "SELECT SCHEMA_NAME(schema_id) + '.' + name FROM sys.tables WHERE is_ms_shipped = 0;"

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $schemaQuery

    $actualTables = @()

    try {

        $reader = $cmd.ExecuteReader()

        try {

            while ($reader.Read()) {
                $actualTables += $reader.GetString(0)
            }

        }
        finally {

            $reader.Dispose()

        }

    }
    finally {

        $cmd.Dispose()

    }

    $expectedQualifiedTables = @(
        $expectedTables | ForEach-Object { "dbo.$_" }
    )

    $unexpectedTables = @(
        $actualTables | Where-Object {
            $_ -notin $expectedQualifiedTables
        }
    )

    $missingTables = @(
        $expectedQualifiedTables | Where-Object {
            $_ -notin $actualTables
        }
    )

    if ($unexpectedTables.Count -gt 0) {
        throw "Unexpected tables detected: $($unexpectedTables -join ', ')"
    }

    if ($missingTables.Count -gt 0) {
        throw "Required tables missing: $($missingTables -join ', ')"
    }

    Write-Host "Database schema validation passed." -ForegroundColor Green

    Write-Host ""
    Write-Host "DATABASE RESET PREVIEW" -ForegroundColor Yellow

    $preview = @()

    foreach ($table in $tablesToReset) {

        $count = Invoke-ScalarQuery -Query "SELECT COUNT_BIG(*) FROM dbo.[$table];"

        $preview += [PSCustomObject]@{
            TableName = $table
            Records   = [long]$count
            Action    = "DELETE"
        }
    }

    foreach ($table in $tablesToPreserve) {

        $count = Invoke-ScalarQuery -Query "SELECT COUNT_BIG(*) FROM dbo.[$table];"

        $preview += [PSCustomObject]@{
            TableName = $table
            Records   = [long]$count
            Action    = "PRESERVE"
        }
    }

    $preview | Format-Table -AutoSize

    $totalDelete = (
        $preview |
        Where-Object { $_.Action -eq "DELETE" } |
        Measure-Object -Property Records -Sum
    ).Sum

    $totalPreserve = (
        $preview |
        Where-Object { $_.Action -eq "PRESERVE" } |
        Measure-Object -Property Records -Sum
    ).Sum

    Write-Host ""
    Write-Host "Records to delete   : $totalDelete"
    Write-Host "Records to preserve : $totalPreserve"

    if (-not $Execute) {

        Write-Host ""
        Write-Host "PREVIEW MODE - No records were deleted." -ForegroundColor Yellow
        return

    }

    if (-not $PSCmdlet.ShouldProcess(
        "$server / $database",
        "Delete integration test records"
    )) {

        Write-Host "Database reset cancelled. No records were deleted."
        return

    }

    Write-Host ""
    Write-Host "Starting transactional database reset..." -ForegroundColor Yellow

    $transaction = $conn.BeginTransaction(
        [System.Data.IsolationLevel]::Serializable
    )

    $preservedCounts = @{}

    foreach ($table in $tablesToPreserve) {

        $preservedCounts[$table] = [long](
            Invoke-ScalarQuery -Query "SELECT COUNT_BIG(*) FROM dbo.[$table];"
        )

    }

    foreach ($table in $tablesToReset) {

        $affected = Invoke-NonQuery -Query "DELETE FROM dbo.[$table];"

        Write-Host ("{0,-40} Deleted: {1}" -f $table, $affected)

    }

    Write-Host ""
    Write-Host "Validating database reset..." -ForegroundColor Yellow

    foreach ($table in $tablesToReset) {

        $remaining = Invoke-ScalarQuery -Query "SELECT COUNT_BIG(*) FROM dbo.[$table];"

        if ($remaining -ne 0) {
            throw "Cleanup validation failed for table: $table"
        }

    }

    foreach ($table in $tablesToPreserve) {

        $remaining = Invoke-ScalarQuery -Query "SELECT COUNT_BIG(*) FROM dbo.[$table];"

        if ($remaining -ne $preservedCounts[$table]) {
            throw "Preserved table validation failed for table: $table"
        }

    }

    $transaction.Commit()

    Write-Host ""
    Write-Host "Database reset completed successfully." -ForegroundColor Green
    Write-Host "Database schema and migration history preserved." -ForegroundColor Green

}
catch {

    if ($null -ne $transaction) {

        try {

            $transaction.Rollback()

            Write-Host ""
            Write-Host "Transaction rolled back." -ForegroundColor Yellow

        }
        catch {

            Write-Warning "Unable to confirm transaction rollback."

        }

    }

    Write-Host ""
    Write-Host "Database reset failed." -ForegroundColor Red

    throw

}
finally {

    if ($null -ne $transaction) {
        $transaction.Dispose()
    }

    $conn.Dispose()

}