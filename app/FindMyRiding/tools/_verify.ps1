$adb = "D:\documents\GitHub\FindMyRiding\tools\platform-tools\adb.exe"
Write-Host "waiting 115s for extraction + loading..."
Start-Sleep -Seconds 115
Write-Host "=== FMR logcat ==="
& $adb logcat -d 2>&1 | Select-String -Pattern "FMR" | ForEach-Object { $_.Line }
Write-Host "=== uiautomator ==="
& $adb shell uiautomator dump 2>&1 | Select-String -Pattern "window_dump" | ForEach-Object { $_.Line }
Write-Host "=== text in dump ==="
& $adb pull /sdcard/window_dump.xml D:\documents\GitHub\FindMyRiding\_ui2.xml 2>&1 | Select-String -Pattern "file pulled" | ForEach-Object { $_.Line }
if (Test-Path D:\documents\GitHub\FindMyRiding\_ui2.xml) {
    $xml = Get-Content D:\documents\GitHub\FindMyRiding\_ui2.xml -Raw
    [regex]::Matches($xml, 'text="[^"]*"') | ForEach-Object { $_.Value }
}
