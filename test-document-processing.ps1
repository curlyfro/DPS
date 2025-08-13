# Test Document Processing System
Write-Host "Testing Document Processing System" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green

# Check database tables
Write-Host "`nChecking database tables..." -ForegroundColor Yellow
$query = @"
SELECT 
    (SELECT COUNT(*) FROM Documents) as DocumentCount,
    (SELECT COUNT(*) FROM ProcessingQueues) as QueueCount,
    (SELECT COUNT(*) FROM ProcessingQueues WHERE Status = 0) as PendingCount,
    (SELECT COUNT(*) FROM ProcessingQueues WHERE Status = 1) as ProcessingCount,
    (SELECT COUNT(*) FROM ProcessingQueues WHERE Status = 2) as CompletedCount,
    (SELECT COUNT(*) FROM ProcessingQueues WHERE Status = 3) as FailedCount,
    (SELECT COUNT(*) FROM Documents WHERE Status = 4) as ProcessedDocs
"@

$connectionString = "Server=(localdb)\mssqllocaldb;Database=DocumentProcessorDB;Trusted_Connection=True;"
$result = Invoke-Sqlcmd -Query $query -ConnectionString $connectionString -TrustServerCertificate

Write-Host "Documents in database: $($result.DocumentCount)" -ForegroundColor Cyan
Write-Host "Queue items total: $($result.QueueCount)" -ForegroundColor Cyan
Write-Host "  - Pending: $($result.PendingCount)" -ForegroundColor White
Write-Host "  - Processing: $($result.ProcessingCount)" -ForegroundColor Yellow
Write-Host "  - Completed: $($result.CompletedCount)" -ForegroundColor Green
Write-Host "  - Failed: $($result.FailedCount)" -ForegroundColor Red
Write-Host "Processed documents: $($result.ProcessedDocs)" -ForegroundColor Green

# Check recent processing activity
Write-Host "`nRecent processing activity:" -ForegroundColor Yellow
$recentQuery = @"
SELECT TOP 5
    pq.Id,
    pq.DocumentId,
    d.FileName,
    pq.Status,
    pq.ProcessedAt,
    CASE pq.Status
        WHEN 0 THEN 'Pending'
        WHEN 1 THEN 'Processing'
        WHEN 2 THEN 'Completed'
        WHEN 3 THEN 'Failed'
        ELSE 'Unknown'
    END as StatusName
FROM ProcessingQueues pq
INNER JOIN Documents d ON pq.DocumentId = d.Id
ORDER BY pq.CreatedAt DESC
"@

$recent = Invoke-Sqlcmd -Query $recentQuery -ConnectionString $connectionString -TrustServerCertificate
$recent | Format-Table -AutoSize

# Check document status
Write-Host "`nDocument status breakdown:" -ForegroundColor Yellow
$statusQuery = @"
SELECT 
    CASE Status
        WHEN 0 THEN 'Draft'
        WHEN 1 THEN 'Uploaded'
        WHEN 2 THEN 'Queued'
        WHEN 3 THEN 'Processing'
        WHEN 4 THEN 'Processed'
        WHEN 5 THEN 'Failed'
        WHEN 6 THEN 'Archived'
        ELSE 'Unknown'
    END as StatusName,
    COUNT(*) as Count
FROM Documents
GROUP BY Status
ORDER BY Status
"@

$statuses = Invoke-Sqlcmd -Query $statusQuery -ConnectionString $connectionString -TrustServerCertificate
$statuses | Format-Table -AutoSize

Write-Host "`nConfiguration Check:" -ForegroundColor Yellow
$configPath = "src\DocumentProcessor.Web\DocumentProcessor.Web\appsettings.json"
$config = Get-Content $configPath | ConvertFrom-Json
Write-Host "AI Provider: $($config.AIProcessing.DefaultProvider)" -ForegroundColor Cyan
Write-Host "Bedrock Mock Mode: $($config.Bedrock.UseMockResponses)" -ForegroundColor Cyan
Write-Host "Bedrock Region: $($config.Bedrock.Region)" -ForegroundColor Cyan
Write-Host "Storage Provider: $($config.DocumentStorage.Provider)" -ForegroundColor Cyan

Write-Host "`n=================================" -ForegroundColor Green
Write-Host "Test Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
if ($result.ProcessedDocs -gt 0) {
    Write-Host "✓ Document processing is working! $($result.ProcessedDocs) documents have been processed." -ForegroundColor Green
} elseif ($result.CompletedCount -gt 0) {
    Write-Host "✓ Queue processing is working! $($result.CompletedCount) items completed." -ForegroundColor Green
    Write-Host "  Check if documents are updating to 'Processed' status." -ForegroundColor Yellow
} elseif ($result.QueueCount -gt 0) {
    Write-Host "⚠ Documents are queued but not yet processed." -ForegroundColor Yellow
    Write-Host "  Make sure the application is running." -ForegroundColor Yellow
} else {
    Write-Host "⚠ No documents in the processing queue." -ForegroundColor Yellow
    Write-Host "  Upload a document through the web interface to test." -ForegroundColor Yellow
}