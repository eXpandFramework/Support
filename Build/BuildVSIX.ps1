Param (
    [string]$XpandFolder=(Get-XpandPath),
    [string]$msbuild="C:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe",
    [string]$DXVersion="0.0.0.0"
)

#update version in templates
$version=New-Object System.Version ($DXVersion)
Write-Host "version=$version"
Get-ChildItem "$XpandFolder\Xpand.Plugins\Xpand.VSIX\ProjectTemplates\*.zip" -Recurse |foreach{
    $tempPath="$(Split-Path $_ -Parent)\temp"
    Expand-Archive -Force $_ -DestinationPath $tempPath
    
    $vsTemplate=(Get-ChildItem $tempPath -Filter *.vstemplate | Select -First 1).FullName
    $content=Get-Content $vsTemplate
    $content = $content -ireplace 'eXpandFramework v([^ ]*)', "eXpandFramework v$($version.Major).$($version.Minor)"
    $content = $content -ireplace 'Xpand.VSIX, Version=([^,]*)', "Xpand.VSIX, Version=$($version.ToString())"
    Set-Content $vsTemplate $content
    Get-ChildItem $tempPath | Compress-Archive -DestinationPath $_ -Force 
    Remove-Item $tempPath -Recurse -Force
}
    

#restore nuget
$fileName="$XpandFolder\Xpand.Plugins\Xpand.VSIX\Xpand.VSIX.csproj"
$nugetExe="$XpandFolder\Support\Tool\nuget.exe"
$expression="$nugetExe restore $fileName"
Write-Host $expression
Invoke-Expression $expression

#upgrade csproj to dotnet 4.6
Copy-Item $fileName -Destination $fileName"Copy" -Force
$package=(Split-Path $fileName -Parent)+"\packages.config"
Copy-Item $package -Destination $package"Copy" -Force
$project=[xml](Get-Content $fileName)
$ns = New-Object System.Xml.XmlNamespaceManager($project.NameTable)
$ns.AddNamespace("ns", $project.DocumentElement.NamespaceURI)

$project.SelectNodes("//ns:TargetFrameworkVersion",$ns)|foreach{
    $_.InnerText="v4.6"
}
$project.Save($fileName);

#upgrade nuget to latest version
$expression="$nugetExe update $fileName -FileConflictAction Ignore"
Write-Host $expression
Invoke-Expression $expression

#build VSIX
& "$msbuild" "$fileName" "/p:Configuration=Release;DeployExtension=false" 

#reset enviroment
Move-Item $fileName"Copy" -Destination $fileName -Force
Move-Item $package"Copy" -Destination $package -Force
Pop-Location


