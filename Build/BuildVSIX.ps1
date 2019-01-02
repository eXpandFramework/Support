Param (
    [string]$XpandFolder=(Get-Item "$PSScriptRoot\..\..").FullName,
    [string]$msbuild="C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\msbuild.exe",
    [string]$DXVersion="0.0.0.0"
)
$ErrorActionPreference = "Stop"
Import-Module "$PSScriptRoot\XpandPosh.psm1" -Force
if ($DXVersion -eq "0.0.0.0"){
    $DXVersion=Get-VersionFromFile "$PSScriptRoot\..\..\Xpand\Xpand.Utils\Properties\XpandAssemblyInfo.cs"
}

#update version in templates
$version=New-Object System.Version ($DXVersion)

Get-ChildItem "$XpandFolder\Xpand.Plugins\Xpand.VSIX\ProjectTemplates\*.zip" -Recurse |ForEach-Object{
    $tempPath="$(Split-Path $_ -Parent)\temp"
    Expand-Archive -Force $_ -DestinationPath $tempPath
    
    $vsTemplate=(Get-ChildItem $tempPath -Filter *.vstemplate | Select-Object -First 1).FullName
    $content=Get-Content $vsTemplate
    $content = $content -ireplace 'eXpandFramework v([^ ]*)', "eXpandFramework v$($version.Major).$($version.Minor)"
    $content = $content -ireplace 'Xpand.VSIX, Version=([^,]*)', "Xpand.VSIX, Version=$($version.ToString())"
    Set-Content $vsTemplate $content
    Get-ChildItem $tempPath | Compress-Archive -DestinationPath $_ -Force 
    Remove-Item $tempPath -Recurse -Force
}

Get-ChildItem "$XpandFolder\Xpand.Plugins\Xpand.VSIX\ProjectTemplates\*.vstemplate" -Recurse|ForEach-Object{
    $content=Get-Content $_
    $content = $content -ireplace "TemplateWizard.v([^,]*),", "TemplateWizard.v$($version.Major).$($version.Minor),"
    Set-Content $_ $content
}

#build VSIX
$fileName="$XpandFolder\Xpand.Plugins\Xpand.VSIX\Xpand.VSIX.csproj"
& "$XpandFolder\Support\Tool\nuget.exe" Restore $fileName -PackagesDirectory "$XpandFolder\Support\_third_party_assemblies\Packages"
& "$msbuild" "$fileName" "/p:Configuration=Release;DeployExtension=false;OutputPath=$XpandFolder\Xpand.Dll\Plugins" /v:m /WarnAsError




