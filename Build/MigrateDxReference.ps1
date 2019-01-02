param(
    [string]$rootPath=$(get-item "$PSScriptRoot\..\..").FullName,
    [string[]]$packageSources=@("https://api.nuget.org/v3/index.json","https://xpandnugetserver.azurewebsites.net/nuget","C:\Program Files (x86)\DevExpress 18.2\Components\System\Components\packages"),
    [string]$version="18.2.300.1"
)
$ErrorActionPreference="Stop"
import-module "$PSScriptRoot\XpandPosh.psm1" -Force
. "$PSScriptRoot\Utils.ps1"
Get-ChildItem $rootPath "*.csproj" -Recurse|ForEach-Object{
    $projectPath=$_.FullName
    Write-Host "Checking DX references $projectPath"
    
    $projectDir=(Get-Item $projectPath).DirectoryName
    
    [xml]$csproj=Get-Content $projectPath
    $packagesDir="$rootPath\Support\_third_party_assemblies\Packages"
    $packagesConfigPath="$projectDir\packages.config"
    
    $packageInstalled=Test-Path "$packagesConfigPath"
    [xml]$packagesConfig=$null
    if ($packageInstalled){
        $packagesConfig=Get-Content $packagesConfigPath
    }
    
    $references=$csproj.Project.ItemGroup.Reference|Where-Object{"$($_.Include)".StartsWith("DevExpress")}|
    Where-Object{!"$($_.Include)".Contains(".DXCore.")}
    
    $references|ForEach-Object{
        $reference=$_
        $include=$reference.Include -creplace '(\.v[\d]{2}\.[\d]{1})', ''
        $packageName=GetPackageName $include
        if ($packageName){
            $v=New-Object System.Version $version
            $sources=[System.String]::Join(";",$packageSources)
            $version="$($v.Major).$($v.Minor).$($v.Build.ToString().Substring(0,1))"
            if ($packageInstalled){
                $packageInstalled=($packagesConfig.packages.Package.Id|Where-Object{$_ -eq $packageName}).Count -gt 0
            }
            $outputPath="$packagesDir\$packageName.$version\lib\net452"
            if (!$packageInstalled){
                
                if (!$(Test-path $outputPath)){
                    Write-Host "Installing $packageName" -f "Green"
                    $r=New-Command "Nuget" "$rootpath\Support\Tool\Nuget.exe" "Install $packageName -source ""$sources"" -ConfigFile $rootpath\Nuget.config -version $version"
                    if ($r.ExitCode){
                        throw $r.stderr
                    }
                    if ($r.stdout.Contains("invalid")){
                        throw $r.stdout
                    }
                }
                if (((($csproj.Project.ItemGroup.None.Include|Where{$_ -eq "packages.config"}).Count -eq 0 -or !$(Test-Path $packagesConfigPath)))){
                    $itemGroup=$csproj.CreateElement("ItemGroup", $csproj.DocumentElement.NamespaceURI)
                    $csproj.Project.AppendChild($itemGroup)|out-null
                    $none=$csproj.CreateElement("None", $csproj.DocumentElement.NamespaceURI)
                    $none.SetAttribute("Include","packages.config")
                    $itemGroup.AppendChild($none)|out-null    
                    Set-Content "$projectDir\packages.config" "﻿<?xml version=""1.0"" encoding=""utf-8""?>`r`n<packages>`r`n</packages>"
                    $packagesConfig=Get-Content "$projectDir\packages.config"
                }

                $package=$packagesConfig.CreateElement("package",$packagesConfig.DocumentElement.NamespaceURI)
                $packagesConfig.SelectSingleNode("//packages").AppendChild($package)|out-null
                $package.SetAttribute("id",$packageName)
                $package.SetAttribute("version",$version)
                $package.SetAttribute("targetFramework","net461")
                $packagesConfig.Save($packagesConfigpath)
                $csproj.Save($projectPath)   
    
                Write-Host "Updating package $include" -f "Cyan"
                if (!$reference.Hintpath){
                    $reference.AppendChild($reference.OwnerDocument.CreateElement("HintPath", $csproj.DocumentElement.NamespaceURI))|out-null
                }
    
                $hintPath=Get-RelativePath $projectPath $outputPath
                $reference.HintPath="$hintPath\$($reference.Include).dll"
                if (!$(Test-path $("$projectDir\$hintPath"))){
                    throw "File not found $($reference.HintPath)"
                }
    
                $csproj.Save($projectPath)
            }
        }
        
    }

}

