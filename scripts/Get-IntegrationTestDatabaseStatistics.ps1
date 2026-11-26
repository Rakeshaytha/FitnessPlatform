[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$server = "(localdb)\MSSQLLocalDB"
$database = "FitnessPlatformIntegrationTestsDb"

$connectionString = "Server=$server;Database=$database;Integrated Security=True;TrustServerCertificate=True;"

$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = $connectionString

function Invoke-DatabaseQuery {
    param(
        [string]$Query
    )

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $Query
    $cmd.CommandTimeout = 60

    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $table = New-Object System.Data.DataTable

    try {
        [void]$adapter.Fill($table)
        return ,$table
    }
    finally {
        $adapter.Dispose()
        $cmd.Dispose()
    }
}

try {

    Write-Host ""
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host " FITNESSPLATFORM INTEGRATION DATABASE AUDIT " -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor Cyan

    $conn.Open()

    if ($conn.Database -ne $database -or $conn.DataSource -ne $server) {
        throw "Unsafe database connection detected."
    }

    Write-Host ""
    Write-Host "Server   : $server"
    Write-Host "Database : $database"
    Write-Host "Date     : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

    Write-Host ""
    Write-Host "DATABASE FILE STATISTICS" -ForegroundColor Yellow

    $fileQuery = @"
SELECT
    name AS LogicalName,
    type_desc AS FileType,
    CAST(size * 8.0 / 1024 AS DECIMAL(18,2)) AS SizeMB
FROM sys.database_files
ORDER BY type;
"@

    $fileStats = Invoke-DatabaseQuery -Query $fileQuery

    $fileStats | Format-Table -AutoSize

    Write-Host ""
    Write-Host "TABLE STATISTICS" -ForegroundColor Yellow

    $tableQuery = @"
SELECT
    SCHEMA_NAME(t.schema_id) AS SchemaName,
    t.name AS TableName,
    SUM(
        CASE
            WHEN p.index_id IN (0,1)
            THEN p.row_count
            ELSE 0
        END
    ) AS TotalRecords,
    CAST(
        SUM(p.reserved_page_count) * 8.0 / 1024
        AS DECIMAL(18,2)
    ) AS ReservedMB,
    CAST(
        SUM(p.used_page_count) * 8.0 / 1024
        AS DECIMAL(18,2)
    ) AS UsedMB
FROM sys.tables t
INNER JOIN sys.dm_db_partition_stats p
    ON t.object_id = p.object_id
GROUP BY
    t.schema_id,
    t.name
ORDER BY
    ReservedMB DESC;
"@

    $tableStats = Invoke-DatabaseQuery -Query $tableQuery

    $tableStats | Format-Table -AutoSize

    $totalRecords = ($tableStats.Rows | Measure-Object -Property TotalRecords -Sum).Sum

    $totalReservedMB = ($tableStats.Rows | Measure-Object -Property ReservedMB -Sum).Sum

    $totalUsedMB = ($tableStats.Rows | Measure-Object -Property UsedMB -Sum).Sum

    $totalDatabaseMB = ($fileStats.Rows | Measure-Object -Property SizeMB -Sum).Sum

    Write-Host ""
    Write-Host "DATABASE SUMMARY" -ForegroundColor Yellow

    Write-Host "Total Tables       : $($tableStats.Rows.Count)"
    Write-Host "Total Records      : $totalRecords"
    Write-Host "Reserved Table MB  : $totalReservedMB"
    Write-Host "Used Table MB      : $totalUsedMB"
    Write-Host "Allocated DB MB    : $totalDatabaseMB"

    Write-Host ""
    Write-Host "EF CORE MIGRATION HISTORY" -ForegroundColor Yellow

    $migrationQuery = @"
SELECT
    MigrationId,
    ProductVersion
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;
"@

    $migrationStats = Invoke-DatabaseQuery -Query $migrationQuery

    $migrationStats | Format-Table -AutoSize

    Write-Host "Total Migrations : $($migrationStats.Rows.Count)"

    Write-Host ""
    Write-Host "Database audit completed successfully." -ForegroundColor Green

}
catch {

    Write-Host ""
    Write-Host "Database audit failed." -ForegroundColor Red

    throw

}
finally {

    $conn.Dispose()

}