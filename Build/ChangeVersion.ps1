
Param (
    [string]$XpandFolder=(Get-XpandPath),
    [string]$Version="0.0.0.1"
)
function Get-XpandVersion ($XpandPath) { 
    $assemblyInfo="$XpandPath\Xpand\Xpand.Utils\Properties\XpandAssemblyInfo.cs"
    $matches = Get-Content $assemblyInfo -ErrorAction Stop | Select-String 'public const string Version = \"([^\"]*)'
    if ($matches) {
        return $matches[0].Matches.Groups[1].Value
    }
    else{
        Write-Error "Version info not found in $assemblyInfo"
    }
}
$assemblyInfo="$XpandFolder\Xpand\Xpand.Utils\Properties\XpandAssemblyInfo.cs"
(Get-Content $assemblyInfo).replace($(Get-XpandVersion $XpandFolder), $Version) | Set-Content $assemblyInfo


