
Param (
    [string]$apiKey=$null,
    [string]$source="https://api.nuget.org/v3/index.json"
)    
$currentLocation=Get-Location
$basePath=[System.IO.Path]::GetFullPath( "$PSScriptRoot\..\..\")
Set-Location $basePath
$nuspecFiles= "$basePath/Support/Nuspec"
$assemblyInfo="$basePath\Xpand\Xpand.Utils\Properties\XpandAssemblyInfo.cs"
$matches = Get-Content $assemblyInfo -ErrorAction Stop | Select-String 'public const string Version = \"([^\"]*)'
# $XpandVersion=$matches[0].Matches.Groups[1].Value 
$XpandVersion="18.2.301.35" 
$nupkgPath= "$PSScriptRoot\..\..\Build\Nuget"
$nupkgPath=[System.IO.Path]::GetFullPath($nupkgPath)
$nugetExe=[System.IO.Path]::GetFullPath( $PSScriptRoot+"\..\Tool\nuget.exe")

Set-Location $nupkgPath
$packages=Get-ChildItem -Path $nupkgPath -Filter *.nupkg
$paramObject = [pscustomobject] @{
    apiKey=$apiKey
    nugetExe=$nugetExe
    source=$source
}
Import-Module "$PSScriptRoot\XpandPosh.psm1" -Force
$modules=(Get-Module XpandPosh).Path
$sb={
    param($parameter)
    
    $params="push $($_.FullName) $($parameter.apiKey) -source $($parameter.source)"
    $result=New-Command $_ $parameter.nugetExe $params $parameter.location
    [PSCustomObject]@{
        result = $result
        project=$_
    } 
}
$packages|start-rsjob  $sb -argumentlist $paramObject -Throttle $throttle -ModulesToImport $modules   |Wait-RSJob -ShowProgress |ForEach-Object{
    $j=Get-RSJob $_  |Receive-RSJob 
    $j.result.stdout
    $j.result.commandTitle
    if ($j.result.ExitCode){
        throw "Fail to push $($j.result.CommandTitle)`n`r$($j.result.stderr)" 
    }
    else{
        Write-Host "Project $($j.result.commandTitle) build succefully" -f "Green"
    }
}   




    