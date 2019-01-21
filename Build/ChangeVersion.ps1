
Param (
    [string]$XpandFolder=$(get-item "$PSScriptRoot\..\..").FullName,
    [string]$Version="0.0.0.1"
)
& "$PSSCriptRoot\ImportXpandPosh.ps1" 
$assemblyInfo="$XpandFolder\Xpand\Xpand.Utils\Properties\XpandAssemblyInfo.cs"
$xpandVersion=Get-XpandVersion $XpandFolder
Write-Host "xpcandVersion=$xpandVersion ,$Version"
(Get-Content $assemblyInfo).replace($xpandVersion, $Version) | Set-Content $assemblyInfo


