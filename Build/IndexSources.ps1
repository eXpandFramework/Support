param(
    $version="18.2"
)

Disable-XExecutionPolicy
$root=(get-item "$PSScriptRoot\..\..\").FullName
$pdbPath="$root\Xpand.DLL\pdb"
if (Test-Path $pdbPath){
    Get-ChildItem $pdbPath -Recurse|Remove-Item -Force
}
New-Item -ItemType Directory -Force -Path $pdbPath
$version=Get-XDevExpressVersion $version
Get-ChildItem "$root\Xpand.Dll" -Include "Xpand.*.pdb" -Exclude "Xpand.XAF.*.pdb" -Recurse |Copy-Item -Destination $pdbPath 

Update-XSymbols -symbolsfolder $pdbPath -user eXpand -repository eXpand -branch $version -sourcesRoot $root  -dbgToolsPath "$root\Support\Tool\srcsrv" 

Copy-Item -path $pdbPath -include "Xpand.*.pdb" -Destination $root -Force
Get-ChildItem $pdbPath -Recurse|Remove-Item -Force