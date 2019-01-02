
Param (
    [string]$XpandFolder=$(get-item "$PSScriptRoot\..\..").FullName,
    [string]$Version="0.0.0.1"
)
Import-Module "$PSSCriptRoot\XpandPosh.psm1" -Force
$assemblyInfo="$XpandFolder\Xpand\Xpand.Utils\Properties\XpandAssemblyInfo.cs"
$xpandVersion=Get-XpandVersion $XpandFolder
Write-Host "xpcandVersion=$xpandVersion ,$Version"
(Get-Content $assemblyInfo).replace($xpandVersion, $Version) | Set-Content $assemblyInfo


