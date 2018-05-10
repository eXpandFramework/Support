Param (
    [string]$XpandFolder=(Get-XpandPath),
    [string]$msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\msbuild.exe",
    [string]$DXVersion="0.0.0.0"
)
. "$PSScriptRoot\Utils.ps1"

if ($DXVersion -eq "0.0.0.0"){
    $DXVersion=Get-Version -path "$PSScriptRoot\..\..\"
}
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

Get-ChildItem "$XpandFolder\Xpand.Plugins\Xpand.VSIX\ProjectTemplates\*.vstemplate" -Recurse|foreach{
    $content=Get-Content $_
    $content = $content -ireplace "TemplateWizard.v([^,]*),", "TemplateWizard.v$($version.Major).$($version.Minor),"
    Set-Content $_ $content
}


$content=Get-Content $vsTemplate
$content = $content -ireplace 'eXpandFramework v([^ ]*)', "eXpandFramework v$($version.Major).$($version.Minor)"

#restore nuget
$fileName="$XpandFolder\Xpand.Plugins\Xpand.VSIX\Xpand.VSIX.csproj"
$nugetExe="$XpandFolder\Support\Tool\nuget.exe"
$expression="$nugetExe restore $fileName"
Write-Host $expression
Invoke-Expression $expression


#build VSIX
& "$msbuild" "$fileName" "/p:Configuration=Release;DeployExtension=false" 




