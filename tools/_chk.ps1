$adb = "D:\documents\GitHub\FindMyRiding\tools\platform-tools\adb.exe"
Start-Sleep -Seconds 30
Write-Host "=== FMR (app) logs ==="
& $adb logcat -d 2>&1 | Select-String -Pattern "FMR" | ForEach-Object { $_.Line }
Write-Host "=== end ==="
