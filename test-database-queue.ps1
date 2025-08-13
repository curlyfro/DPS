# Test Database Queue Implementation
Write-Host "Database Queue Test Script" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host ""

# Check if ProcessingQueues table has data
Write-Host "Checking ProcessingQueues table..." -ForegroundColor Yellow
$connectionString = "Server=(localdb)\mssqllocaldb;Database=DocumentProcessorDb;Trusted_Connection=True;"

try {
    $query = @"
SELECT 
    Id,
    DocumentId,
    ProcessingType,
    Status,
    Priority,
    RetryCount,
    CreatedAt,
    StartedAt,
    CompletedAt,
    ErrorMessage
FROM ProcessingQueues
ORDER BY CreatedAt DESC
"@

    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    $command = $connection.CreateCommand()
    $command.CommandText = $query
    
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataset) | Out-Null
    
    $results = $dataset.Tables[0]
    
    if ($results.Rows.Count -eq 0) {
        Write-Host "No items found in ProcessingQueues table" -ForegroundColor Red
        Write-Host ""
        Write-Host "This means documents are not being queued to the database." -ForegroundColor Yellow
        Write-Host "Possible issues:" -ForegroundColor Yellow
        Write-Host "  1. DocumentUpload page is still using old in-memory queue" -ForegroundColor White
        Write-Host "  2. Database queue service is not registered properly" -ForegroundColor White
        Write-Host "  3. Error during queue insertion" -ForegroundColor White
    }
    else {
        Write-Host "Found $($results.Rows.Count) items in ProcessingQueues table:" -ForegroundColor Green
        Write-Host ""
        
        foreach ($row in $results.Rows) {
            $statusText = switch ($row.Status) {
                0 { "Pending" }
                1 { "InProgress" }
                2 { "Completed" }
                3 { "Failed" }
                4 { "Retrying" }
                5 { "Cancelled" }
                6 { "Skipped" }
                default { "Unknown" }
            }
            
            Write-Host "Queue Item:" -ForegroundColor Cyan
            Write-Host "  ID: $($row.Id)"
            Write-Host "  Document ID: $($row.DocumentId)"
            Write-Host "  Status: $statusText ($($row.Status))"
            Write-Host "  Priority: $($row.Priority)"
            Write-Host "  Created: $($row.CreatedAt)"
            if ($row.StartedAt -ne [DBNull]::Value) {
                Write-Host "  Started: $($row.StartedAt)"
            }
            if ($row.CompletedAt -ne [DBNull]::Value) {
                Write-Host "  Completed: $($row.CompletedAt)"
            }
            if ($row.ErrorMessage -ne [DBNull]::Value) {
                Write-Host "  Error: $($row.ErrorMessage)" -ForegroundColor Red
            }
            Write-Host ""
        }
    }
    
    # Also check Documents table status
    Write-Host "Checking Documents table status..." -ForegroundColor Yellow
    $docQuery = @"
SELECT 
    Id,
    FileName,
    Status,
    CreatedAt,
    ProcessedAt
FROM Documents
ORDER BY CreatedAt DESC
OFFSET 0 ROWS FETCH NEXT 5 ROWS ONLY
"@
    
    $command.CommandText = $docQuery
    $docAdapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $docDataset = New-Object System.Data.DataSet
    $docAdapter.Fill($docDataset) | Out-Null
    
    $docResults = $docDataset.Tables[0]
    
    Write-Host "Recent documents:" -ForegroundColor Green
    foreach ($row in $docResults.Rows) {
        $statusText = switch ($row.Status) {
            0 { "Pending" }
            1 { "Uploaded" }
            2 { "Queued" }
            3 { "Processing" }
            4 { "Processed" }
            5 { "Failed" }
            6 { "Completed" }
            default { "Unknown" }
        }
        
        Write-Host "  $($row.FileName): Status = $statusText ($($row.Status))" -ForegroundColor White
    }
    
    $connection.Close()
}
catch {
    Write-Host "Error accessing database: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "1. Stop the application (close browser and stop debugging)" -ForegroundColor White
Write-Host "2. Rebuild the solution" -ForegroundColor White
Write-Host "3. Run the application again" -ForegroundColor White
Write-Host "4. Upload a test document" -ForegroundColor White
Write-Host "5. Run this script again to see if items appear in ProcessingQueues table" -ForegroundColor White