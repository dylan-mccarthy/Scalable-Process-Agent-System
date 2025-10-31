# Bulk Dismiss Stale CodeQL Alerts
# This script dismisses alerts for files that no longer exist in the repository

Write-Host "`n=== Bulk Dismiss Stale CodeQL Alerts ===" -ForegroundColor Cyan
Write-Host "This will dismiss alerts for files that don't exist in the repository.`n" -ForegroundColor Yellow

# Load all alerts
$alerts = Get-Content all-alerts.json | ConvertFrom-Json | Where-Object { $_.state -eq 'open' }

# Find stale alerts (files don't exist)
$staleAlerts = $alerts | Where-Object { -not (Test-Path "e:\Scalable-Process-Agent-System\$($_.file)") }

Write-Host "Total open alerts: $($alerts.Count)" -ForegroundColor White
Write-Host "Stale alerts to dismiss: $($staleAlerts.Count)" -ForegroundColor Red
Write-Host "Valid alerts remaining: $($alerts.Count - $staleAlerts.Count)" -ForegroundColor Green

# Group by file type for summary
$dockerAlerts = $staleAlerts | Where-Object {$_.file -like '*dylan-mccarthy*' -or $_.file -like 'app/*'}
$buildAlerts = $staleAlerts | Where-Object {$_.file -like '*/obj/*'}
$nodeAlerts = $staleAlerts | Where-Object {$_.file -like 'usr/*'}

Write-Host "`nBreakdown:" -ForegroundColor Cyan
Write-Host "  Docker/Container alerts: $($dockerAlerts.Count)" -ForegroundColor Yellow
Write-Host "  Build artifact alerts: $($buildAlerts.Count)" -ForegroundColor Yellow
Write-Host "  Node module alerts: $($nodeAlerts.Count)" -ForegroundColor Yellow

Write-Host "`nSample files being dismissed:" -ForegroundColor Cyan
$staleAlerts | Select-Object -ExpandProperty file -Unique | Select-Object -First 10 | ForEach-Object {
    Write-Host "  $_" -ForegroundColor Gray
}

# Ask for confirmation
Write-Host "`n" -NoNewline
$confirm = Read-Host "Do you want to dismiss these $($staleAlerts.Count) stale alerts? (yes/no)"

if ($confirm -ne 'yes') {
    Write-Host "Aborted. No alerts were dismissed." -ForegroundColor Yellow
    exit 0
}

Write-Host "`nDismissing stale alerts..." -ForegroundColor Yellow

$dismissed = 0
$failed = 0
$total = $staleAlerts.Count

foreach ($alert in $staleAlerts) {
    $dismissed++
    $percent = [math]::Round(($dismissed / $total) * 100, 1)
    Write-Progress -Activity "Dismissing Stale Alerts" -Status "$percent% Complete ($dismissed/$total)" -PercentComplete $percent
    
    try {
        $reason = if ($alert.file -like '*dylan-mccarthy*') {
            "Scanned Docker container image, not source code"
        } elseif ($alert.file -like '*/obj/*') {
            "Build artifact, not source code"
        } elseif ($alert.file -like 'usr/*') {
            "Node.js dependency, not source code"
        } elseif ($alert.file -like 'app/*') {
            "Container runtime path, not source code"
        } else {
            "File no longer exists in repository"
        }
        
        gh api -X PATCH "/repos/dylan-mccarthy/Scalable-Process-Agent-System/code-scanning/alerts/$($alert.number)" `
            -f state=dismissed `
            -f dismissed_reason="won't fix" `
            -f dismissed_comment="$reason" `
            --silent
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Dismissed alert #$($alert.number) - $($alert.file)" -ForegroundColor Green
        } else {
            Write-Host "✗ Failed to dismiss alert #$($alert.number)" -ForegroundColor Red
            $failed++
        }
    }
    catch {
        Write-Host "✗ Error dismissing alert #$($alert.number): $_" -ForegroundColor Red
        $failed++
    }
    
    # Rate limiting: Add small delay every 10 requests
    if ($dismissed % 10 -eq 0) {
        Start-Sleep -Milliseconds 100
    }
}

Write-Progress -Activity "Dismissing Stale Alerts" -Completed

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "Successfully dismissed: $($dismissed - $failed)" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
Write-Host "`nRemaining open alerts should now be: $($alerts.Count - $staleAlerts.Count)" -ForegroundColor Yellow
Write-Host "`nRun this to verify:" -ForegroundColor Cyan
Write-Host "  gh api /repos/dylan-mccarthy/Scalable-Process-Agent-System/code-scanning/alerts --jq '[.[] | select(.state == `"open`")] | length'" -ForegroundColor Gray
