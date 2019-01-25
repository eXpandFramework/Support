param(

)
    

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


Get-ChildItem "$basePath/Xpand.DLL" -Include @('*.pdb','*.dll')| Copy-Item -Destination "$basePath\build\temp\$_" 

& "$PSScriptRoot\UpdateNuspecContainers.ps1"

$supportFolder=$(Split-Path $PSScriptRoot)
$XpandFolder=(Get-Item $supportFolder).Parent.FullName
$nuspecFolder="$supportFolder\Nuspec"
Get-ChildItem $nuspecFolder  -Filter "*.nuspec" | foreach{
    $filePath="$nuspecFolder\$_"
    (Get-Content $filePath).replace('src="\Build', "src=`"$XpandFolder\Build") | Set-Content $filePath -Encoding UTF8
}

Remove-Item "$nupkgPath" -Force -Recurse 

$nuspecFiles=Get-ChildItem -Path $nuspecFiles -Filter *.nuspec



workflow Invoke-Pack {
    param ($psObj )
    $complete = 0
    Foreach -parallel ($nuget in $psObj.Nuspecs) { 
        InlineScript {
            Write-Output "Packing $($Using:nuget)"
            & Nuget Pack $Using:nuget -version $Using:psObj.Version -OutputDirectory $Using:psObj.OutputDirectory
        } 
        $Workflow:complete = $Workflow:complete + 1 
        [int]$percentComplete = ($Workflow:complete * 100) / $Workflow:psObj.Nuspecs.Count
        Write-Progress -Id 1 -Activity "Packing" -PercentComplete $percentComplete -Status "$percentComplete% :$($nuget.Name)"
    }
    Write-Progress -Id 1 -Status "Ready" -Activity "Packing" -Completed
    
}

$psObj = [PSCustomObject]@{
    OutputDirectory = $nupkgPath
    Nuspecs          = $nuspecFiles|Select-Object -ExpandProperty FullName 
    version=$XpandVersion
}
Invoke-Pack $psObj

Get-ChildItem $nuspecFolder  -Filter "*.nuspec" | foreach{
    $filePath="$nuspecFolder\$_"
    (Get-Content $filePath).replace("src=`"$XpandFolder\Build",'src="\Build') | Set-Content $filePath -Encoding UTF8
}

$packageDir="$basepath\Build\_package\$XpandVersion"
New-Item $packageDir -ItemType Directory -Force|Out-Null
Compress-XFiles -DestinationPath "$packageDir\Nupkg-$XpandVersion.zip" -path $nupkgPath




    