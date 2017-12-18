$supportFolder=$(Split-Path $PSScriptRoot)
$XpandFolder=(Get-Item $supportFolder).Parent.FullName
$nuspecFolder="$supportFolder\Nuspec"
Get-ChildItem $nuspecFolder  -Filter "*.nuspec" | foreach{
    $filePath="$nuspecFolder\$_"
    Write-Host $filePath
    (Get-Content $filePath).replace('src="\Build', "src=`"$XpandFolder\Build") | Set-Content $filePath -Encoding UTF8
}