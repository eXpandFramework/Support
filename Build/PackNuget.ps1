param(
    $throttle=10
)
    
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
#update agnostic package
& "$PSScriptRoot\UpdateNuspecContainers.ps1"
#modify nuspecs
$supportFolder=$(Split-Path $PSScriptRoot)
$XpandFolder=(Get-Item $supportFolder).Parent.FullName
$nuspecFolder="$supportFolder\Nuspec"
Get-ChildItem $nuspecFolder  -Filter "*.nuspec" | foreach{
    $filePath="$nuspecFolder\$_"
    (Get-Content $filePath).replace('src="\Build', "src=`"$XpandFolder\Build") | Set-Content $filePath -Encoding UTF8
}


#pack
Remove-Item "$nupkgPath" -Force -Recurse 
$paramObject = [pscustomobject] @{
    version=$XpandVersion
    nugetBin=$nupkgPath
    nugetExe=$nugetExe
    basePath=$basePath
}

$nuspecFiles=Get-ChildItem -Path $nuspecFiles -Filter *.nuspec
# & $paramObject.nugetExe pack $nuspecFiles.FullName -version $paramObject.version -OutputDirectory $paramObject.nugetBin 
Import-Module "$PSScriptRoot\XpandPosh.psm1" -Force
$modules=(Get-Module XpandPosh).Path
$sb={
    param($parameter)
    
    $params="pack $($_.FullName) -version $($parameter.version) -OutputDirectory $($parameter.nugetBin)"
    $result=New-Command $_ $parameter.nugetExe $params $parameter.location
    [PSCustomObject]@{
        result = $result
        project=$_
    } 
}
$nuspecFiles|start-rsjob  $sb -argumentlist $paramObject -Throttle $throttle -ModulesToImport $modules   |Wait-RSJob -ShowProgress |ForEach-Object{
    $j=Get-RSJob $_  |Receive-RSJob 
    $j.result.stdout
    $j.result.commandTitle
    if ($j.result.ExitCode){
        throw "Fail to build $($j.result.CommandTitle)`n`r$($j.result.stderr)" 
    }
    else{
        Write-Host "Project $($j.result.commandTitle) build succefully" -f "Green"
    }
}
Get-ChildItem $nuspecFolder  -Filter "*.nuspec" | foreach{
    $filePath="$nuspecFolder\$_"
    (Get-Content $filePath).replace("src=`"$XpandFolder\Build",'src="\Build') | Set-Content $filePath -Encoding UTF8
}

$packageDir="$basepath\Build\_package\$XpandVersion"
New-Item $packageDir -ItemType Directory -Force|Out-Null
Start-Zip "$packageDir\Nupkg-$XpandVersion.zip" $nupkgPath




    