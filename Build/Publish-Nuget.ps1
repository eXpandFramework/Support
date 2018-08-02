Param (
    [string]$apiKey
)

$currentLocation=Get-Location
$basePath=Resolve-Path "$PSScriptRoot\..\..\"
$nuspecFiles= "$basePath/Support/Nuspec"
$assemblyInfo="$basePath\Xpand\Xpand.Utils\Properties\XpandAssemblyInfo.cs"
$matches = Get-Content $assemblyInfo -ErrorAction Stop | Select-String 'public const string Version = \"([^\"]*)'
$DXVersion=$matches[0].Matches.Groups[1].Value 
$nupkgPath= "$PSScriptRoot\..\..\Build\Nuget"
New-Item $nupkgPath -ItemType Directory -ErrorAction Continue
$nugetExe=Resolve-Path $PSScriptRoot+"\..\Tool\nuget.exe"

Remove-Item "$nupkgPath\*.*" 
Get-ChildItem -Path $nuspecFiles -Filter *.nuspec | foreach{
    $sb= "cmd /c $nugetExe pack $($_.FullName) -OutputDirectory $nupkgPath -BasePath $basePath -Version $DXVersion"
    $expr=Invoke-Expression "$sb"
    
    Write-Host "$_::::$expr"
}

Get-ChildItem -Path $nupkgPath -Filter *.nupkg | foreach{
    $sb= "cmd /c $nugetPath push $_ $($paramObject.apiKey) -source https://api.nuget.org/v3/index.json" 
    $expr=Invoke-Expression $sb
    Write-Host "$_::::$expr"
}


    