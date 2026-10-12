$env:ANDROID_SDK_ROOT = "C:\Users\jakel\AppData\Local\Android\Sdk"
$emulator = "C:\Users\jakel\AppData\Local\Android\Sdk\emulator\emulator.exe"
$arglist = "-avd Wear_OS_Large_Round -no-window -no-snapshot -no-audio -no-boot-anim"
Start-Process -FilePath $emulator -ArgumentList $arglist
Write-Output "emulator launch issued"
