$root = 'D:/documents/GitHub/CanadaRiding/data/Shapefiles'
Get-ChildItem -Path $root -Recurse -File | ForEach-Object {
    Write-Output ($_.FullName.Substring($root.Length + 1))
} | Sort-Object
