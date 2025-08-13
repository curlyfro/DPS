# Test script to verify Bedrock AI processing fixes
Write-Host "Testing Bedrock AI Processing Fixes" -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Green

# Create test documents of various sizes
Write-Host "`nCreating test documents..." -ForegroundColor Yellow

# Small document (should process normally)
$smallDoc = @"
System Information Report
Date: 08/13/2025
Computer: TEST-PC
OS: Windows 11
"@
$smallDoc | Out-File -FilePath "test-small.txt" -Encoding UTF8

# Medium document (should process normally)
$mediumDoc = @"
System Information Report
Date: 08/13/2025  
Time: 00:42:00

Host Name:                 TEST-PC
OS Name:                   Microsoft Windows 11 Pro
OS Version:                10.0.26100 Build 26100
OS Manufacturer:           Microsoft Corporation
OS Configuration:          Standalone Workstation
OS Build Type:             Multiprocessor Free
System Manufacturer:       Dell Inc.
System Model:              OptiPlex 7090
System Type:               x64-based PC
Processor(s):              1 Processor(s) Installed.
                          [01]: Intel64 Family 6 Model 167 Stepping 1 GenuineIntel ~2496 Mhz
BIOS Version:              Dell Inc. 1.14.0, 3/15/2024
Windows Directory:         C:\WINDOWS
System Directory:          C:\WINDOWS\system32
Boot Device:               \Device\HarddiskVolume1
Total Physical Memory:     32,512 MB
Available Physical Memory: 18,234 MB
Virtual Memory: Max Size:  37,376 MB
Virtual Memory: Available: 22,156 MB
Virtual Memory: In Use:    15,220 MB
"@ * 10  # Repeat to make it larger

$mediumDoc | Out-File -FilePath "test-medium.txt" -Encoding UTF8

# Large document (should be truncated)
Write-Host "Creating large test document (this will be truncated)..." -ForegroundColor Yellow
$largeDoc = "This is a test document that will be very large. " * 2000
$largeDoc | Out-File -FilePath "test-large.txt" -Encoding UTF8

Write-Host "`nTest files created:" -ForegroundColor Green
Write-Host "  - test-small.txt ($(([System.IO.FileInfo]"test-small.txt").Length) bytes)" -ForegroundColor Cyan
Write-Host "  - test-medium.txt ($(([System.IO.FileInfo]"test-medium.txt").Length) bytes)" -ForegroundColor Cyan
Write-Host "  - test-large.txt ($(([System.IO.FileInfo]"test-large.txt").Length) bytes)" -ForegroundColor Cyan

Write-Host "`n" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Please upload these files at:" -ForegroundColor Yellow
Write-Host "https://localhost:7266/upload" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "`nMonitor the console for:" -ForegroundColor Green
Write-Host "  1. No JSON parsing errors" -ForegroundColor White
Write-Host "  2. Successful entity extraction" -ForegroundColor White
Write-Host "  3. Proper truncation of large documents" -ForegroundColor White
Write-Host "  4. No 'Input too long' errors" -ForegroundColor White

Write-Host "`nPress Ctrl+C to stop when testing is complete." -ForegroundColor Yellow