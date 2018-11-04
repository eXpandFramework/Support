
Param (
    [string]$apiKey,
    [string]$source="https://api.nuget.org/v3/index.json"
)
. "$PSScriptRoot\zipfiles.ps1"
. "$PSScriptRoot\Invoke-InParallel.ps1"
    
$currentLocation=Get-Location
$basePath=[System.IO.Path]::GetFullPath( "$PSScriptRoot\..\..\")
Set-Location $basePath
$nuspecFiles= "$basePath/Support/Nuspec"
$assemblyInfo="$basePath\Xpand\Xpand.Utils\Properties\XpandAssemblyInfo.cs"
$matches = Get-Content $assemblyInfo -ErrorAction Stop | Select-String 'public const string Version = \"([^\"]*)'
$XpandVersion=$matches[0].Matches.Groups[1].Value 
$nupkgPath= "$PSScriptRoot\..\..\Build\Nuget"
New-Item $nupkgPath -ItemType Directory -ErrorAction SilentlyContinue
$nupkgPath=[System.IO.Path]::GetFullPath($nupkgPath)
Remove-Item "$basePath\build\temp" -Force -Recurse -ErrorAction SilentlyContinue 
New-Item "$basePath\build\temp" -ItemType Directory -ErrorAction SilentlyContinue
$nugetExe=[System.IO.Path]::GetFullPath( $PSScriptRoot+"\..\Tool\nuget.exe")

#copy to temp
Get-ChildItem "$basePath/Xpand.DLL" -Include @('*.pdb','*.dll')| Copy-Item -Destination "$basePath\build\temp\$_" 
#modify nuspecs
$supportFolder=$(Split-Path $PSScriptRoot)
$XpandFolder=(Get-Item $supportFolder).Parent.FullName
$nuspecFolder="$supportFolder\Nuspec"
Get-ChildItem $nuspecFolder  -Filter "*.nuspec" | foreach{
    $filePath="$nuspecFolder\$_"
    Write-Host $filePath
    (Get-Content $filePath).replace('src="\Build', "src=`"$XpandFolder\Build") | Set-Content $filePath -Encoding UTF8
}
#pack
Remove-Item "$nupkgPath" -Force -Recurse -ErrorAction sil 
$paramObject = [pscustomobject] @{
    version=$XpandVersion
    nugetBin=$nupkgPath
    nugetExe=$nugetExe
    basePath="$basePath\build\temp"
}
$nuspecFiles=Get-ChildItem -Path $nuspecFiles -Filter *.nuspec
Invoke-InParallel -InputObject $nuspecFiles -Parameter $paramObject -runspaceTimeout 30  -ScriptBlock {  
    $basePath= "$($parameter.basePath)\$_"
    & $parameter.nugetExe pack $_.FullName -version $parameter.version -OutputDirectory $parameter.nugetBin -Basepath $basePath
}

Set-Location $nupkgPath
if ($apiKey){
    $packages=Get-ChildItem -Path $nupkgPath -Filter *.nupkg
    $paramObject = [pscustomobject] @{
        apiKey=$apiKey
        nugetExe=$nugetExe
        source=$source
    }   
    Invoke-InParallel -InputObject $nuspecFiles -Parameter $paramObject -runspaceTimeout 30  -ScriptBlock {  
        & $parameter.nugetExe push $_.FullName $parameter.apiKey -source $parameter.source
    }
}
Zip-Files
$packageDir="$basepath\Build\_package"
Write-Host "packageDir=$packageDir"
Get-ChildItem $packageDir |foreach{
    Copy-Item "$nupkgPath\nuget.zip" -Destination "$($_.FullName)\Nupkg-$XpandVersion.zip"
}



    