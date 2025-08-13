# Check Document Processing System
Write-Host "`n===== Document Processing System Check =====" -ForegroundColor Cyan
Write-Host "Checking both Documents and ProcessingQueues tables`n" -ForegroundColor Yellow

$connectionString = "Server=(localdb)\mssqllocaldb;Database=DocumentProcessorDb;Trusted_Connection=True;"

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    # Check Documents table
    Write-Host "DOCUMENTS TABLE:" -ForegroundColor Green
    Write-Host "----------------" -ForegroundColor Green
    
    $docQuery = "SELECT Id, FileName, Status, CreatedAt, UpdatedAt FROM Documents ORDER BY CreatedAt DESC"
    $command = $connection.CreateCommand()
    $command.CommandText = $docQuery
    
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataset) | Out-Null
    
    if ($dataset.Tables[0].Rows.Count -eq 0) {
        Write-Host "  No documents found" -ForegroundColor Red
    } else {
        foreach ($row in $dataset.Tables[0].Rows) {
            $statusText = switch ($row.Status) {
                0 { "Pending" }
                1 { "Uploaded" }
                2 { "Queued" }
                3 { "Processing" }
                4 { "Processed" }
                5 { "Failed" }
                6 { "Completed" }
                default { "Unknown($($row.Status))" }
            }
            Write-Host "  Doc: $($row.FileName)" -ForegroundColor White
            Write-Host "    ID: $($row.Id)" -ForegroundColor Gray
            Write-Host "    Status: $statusText" -ForegroundColor Cyan
            Write-Host "    Created: $($row.CreatedAt)" -ForegroundColor Gray
        }
    }
    
    # Check ProcessingQueues table
    Write-Host "`nPROCESSINGQUEUES TABLE:" -ForegroundColor Green
    Write-Host "----------------------" -ForegroundColor Green
    
    $queueQuery = "SELECT Id, DocumentId, Status, Priority, CreatedAt FROM ProcessingQueues ORDER BY CreatedAt DESC"
    $command.CommandText = $queueQuery
    
    $adapter2 = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset2 = New-Object System.Data.DataSet
    $adapter2.Fill($dataset2) | Out-Null
    
    if ($dataset2.Tables[0].Rows.Count -eq 0) {
        Write-Host "  No queue items found" -ForegroundColor Red
        Write-Host "`n  ⚠️  This indicates the database queue is NOT being used!" -ForegroundColor Yellow
    } else {
        foreach ($row in $dataset2.Tables[0].Rows) {
            $statusText = switch ($row.Status) {
                0 { "Pending" }
                1 { "InProgress" }
                2 { "Completed" }
                3 { "Failed" }
                4 { "Retrying" }
                5 { "Cancelled" }
                default { "Unknown($($row.Status))" }
            }
            Write-Host "  Queue Item:" -ForegroundColor White
            Write-Host "    ID: $($row.Id)" -ForegroundColor Gray
            Write-Host "    Document: $($row.DocumentId)" -ForegroundColor Gray
            Write-Host "    Status: $statusText" -ForegroundColor Cyan
            Write-Host "    Priority: $($row.Priority)" -ForegroundColor Gray
            Write-Host "    Created: $($row.CreatedAt)" -ForegroundColor Gray
        }
    }
    
    $connection.Close()
    
    Write-Host "`n===== EXPECTED BEHAVIOR =====" -ForegroundColor Cyan
    Write-Host "When you upload a file:" -ForegroundColor Yellow
    Write-Host "1. A record should appear in Documents table (Status: Uploaded/Pending)" -ForegroundColor White
    Write-Host "2. A record should appear in ProcessingQueues table (Status: Pending)" -ForegroundColor White
    Write-Host "3. Background service processes from ProcessingQueues table" -ForegroundColor White
    Write-Host "4. Both records get updated as processing completes" -ForegroundColor White
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}