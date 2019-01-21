param(
    $version="1.1.1.1"
)
& "$PSScriptRoot\ImportXpandPosh.ps1"
Disable-ExecutionPolicy
$root=(get-item "$PSScriptRoot\..\..\").FullName
$pdbPath="$root\Xpand.DLL\pdb"
New-Item -ItemType Directory -Force -Path $pdbPath

Copy-Item -path "$root\*" -include "Xpand.*.pdb" -Destination $pdbPath 

& "$PSScriptRoot\Sourcepack.ps1" -symbolsfolder $pdbPath -userId eXpand -repository eXpand -branch $version -sourcesRoot $root  -githuburl https://raw.githubusercontent.com -serverIsRaw -dbgToolsPath "$root\Support\Tool\srcsrv" 

Copy-Item -path $pdbPath -include "Xpand.*.pdb" -Destination $root -Force