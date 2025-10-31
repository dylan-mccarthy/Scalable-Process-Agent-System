# Verify CodeQL Alerts - Check if alert locations still exist and show the code
# This script helps verify that all open CodeQL alerts reference actual code

Write-Host "`n=== CodeQL Alert Verification ===" -ForegroundColor Cyan
Write-Host "Checking all 30 open alerts...`n" -ForegroundColor Yellow

$alerts = gh api /repos/dylan-mccarthy/Scalable-Process-Agent-System/code-scanning/alerts --jq '.[] | {number, rule: .rule.id, file: .most_recent_instance.location.path, line: .most_recent_instance.location.start_line, state}' | ConvertFrom-Json

$grouped = $alerts | Group-Object -Property file

foreach ($group in $grouped) {
    $file = $group.Name
    $fullPath = "e:\Scalable-Process-Agent-System\$file"
    $exists = Test-Path $fullPath
    
    Write-Host "File: $file" -ForegroundColor $(if ($exists) { "Green" } else { "Red" })
    Write-Host "  Exists: $exists"
    Write-Host "  Alert Count: $($group.Count)"
    
    if ($exists) {
        $content = Get-Content $fullPath
        foreach ($alert in $group.Group) {
            $lineNum = $alert.line
            $lineContent = if ($lineNum -le $content.Length) { $content[$lineNum - 1].Trim() } else { "LINE OUT OF RANGE" }
            
            Write-Host "    Alert #$($alert.number): $($alert.rule) @ line $lineNum" -ForegroundColor Cyan
            Write-Host "      Code: $lineContent" -ForegroundColor Gray
        }
    } else {
        Write-Host "  ⚠️  FILE DOES NOT EXIST - These alerts should be dismissed!" -ForegroundColor Red
        foreach ($alert in $group.Group) {
            Write-Host "    Alert #$($alert.number): $($alert.rule)" -ForegroundColor Red
        }
    }
    Write-Host ""
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
$totalAlerts = $alerts.Count
$validFiles = ($grouped | Where-Object { Test-Path "e:\Scalable-Process-Agent-System\$($_.Name)" }).Count
$totalFiles = $grouped.Count
$staleAlerts = $alerts | Where-Object { -not (Test-Path "e:\Scalable-Process-Agent-System\$($_.file)") }

Write-Host "Total Open Alerts: $totalAlerts"
Write-Host "Files with Alerts: $totalFiles"
Write-Host "Files that Exist: $validFiles" -ForegroundColor Green
Write-Host "Files Missing: $($totalFiles - $validFiles)" -ForegroundColor $(if ($validFiles -eq $totalFiles) { "Green" } else { "Red" })

if ($staleAlerts) {
    Write-Host "`n⚠️  Stale Alerts Found (files don't exist):" -ForegroundColor Red
    $staleAlerts | ForEach-Object {
        Write-Host "  Alert #$($_.number): $($_.file) - $($_.rule)"
    }
    Write-Host "`nTo dismiss these stale alerts, run:" -ForegroundColor Yellow
    $staleAlerts | ForEach-Object {
        Write-Host "  gh api -X PATCH /repos/dylan-mccarthy/Scalable-Process-Agent-System/code-scanning/alerts/$($_.number) -f state=dismissed -f dismissed_reason=won't fix -f dismissed_comment='File no longer exists in repository'"
    }
} else {
    Write-Host "`n✅ All alerts reference files that exist in the repository!" -ForegroundColor Green
    Write-Host "These are legitimate code quality issues that should be fixed." -ForegroundColor Green
}
