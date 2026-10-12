$adb = "D:\documents\GitHub\FindMyRiding\tools\platform-tools\adb.exe"
Write-Host "waiting 110s for extraction + loading..."
Start-Sleep -Seconds 110
Write-Host "=== FMR logcat ==="
& $adb logcat -d 2>&1 | Select-String -Pattern "FMR" | ForEach-Object { $_.Line }
Write-Host "=== activity ==="
& $adb shell dumpsys activity activities 2>&1 | Select-String -Pattern "mResumedActivity|mFocusedActivity" | ForEach-Object { $_.Line }
