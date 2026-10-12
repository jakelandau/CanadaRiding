$dirs = Get-ChildItem 'D:/documents/GitHub/FindMyRiding/data/Shapefiles/Provincial/QC' -Directory
foreach ($d in $dirs) {
    $n = $d.Name
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($n)
    $hex = ($bytes | ForEach-Object { $_.ToString('X2') }) -join ' '
    Write-Output ("NAME=" + $n)
    Write-Output ("UTF8HEX=" + $hex)
    # expected: circonscriptions_électorales_2026_shapefile
    $expected = 'circonscriptions_' + [char]0xE9 + 'lectorales_2026_shapefile'
    Write-Output ("MATCHES_E9=" + ($n -eq $expected))
}
