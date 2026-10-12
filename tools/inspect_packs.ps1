$ErrorActionPreference = 'Stop'
$root = 'C:/Program Files/dotnet/packs'
Write-Output '=== pack version dirs (packs/<id>/<ver>) ==='
Get-ChildItem -Path $root -Directory -Recurse -Depth 2 | Where-Object { $_.Name -ne 'localize' } | Sort-Object FullName | Select-Object -ExpandProperty FullName
Write-Output '=== template-packs ==='
$tp = Join-Path $root 'template-packs'
if (Test-Path $tp) { Get-ChildItem -Path $tp | Select-Object -ExpandProperty Name } else { Write-Output 'NONE' }
