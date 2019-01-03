function InstallPackage($outputPath, $packageName,$sources,$rootPath, $version,$csproj,$packagesConfigPath,$projectDir,$projectPath){
    $packageInstalled = Test-Path "$packagesConfigPath"
    [xml]$packagesConfig=$null
    if ($packageInstalled) {
        $packagesConfig = Get-Content $packagesConfigPath
    }
    if ($packageInstalled) {
        $packageInstalled = ($packagesConfig.packages.Package.Id|Where-Object {$_ -eq $packageName}).Count -gt 0
    }
    if ($packageInstalled) {
        return
    }
    if (!$(Test-path $outputPath)) {
        Write-Host "Installing $packageName" -f "Green"
        $r = New-Command "Nuget" "$rootpath\Support\Tool\Nuget.exe" "Install $packageName -source ""$sources"" -ConfigFile $rootpath\Nuget.config -version $version"
        if ($r.ExitCode) {
            throw $r.stderr
        }
        if ($r.stdout.Contains("invalid")) {
            throw $r.stdout
        }
    }
    if (((($csproj.Project.ItemGroup.None.Include|Where {$_ -eq "packages.config"}).Count -eq 0 -or !$(Test-Path $packagesConfigPath)))) {
        $itemGroup = $csproj.CreateElement("ItemGroup", $csproj.DocumentElement.NamespaceURI)
        $csproj.Project.AppendChild($itemGroup)|out-null
        $none = $csproj.CreateElement("None", $csproj.DocumentElement.NamespaceURI)
        $none.SetAttribute("Include", "packages.config")
        $itemGroup.AppendChild($none)|out-null    
        Set-Content "$projectDir\packages.config" "﻿<?xml version=""1.0"" encoding=""utf-8""?>`r`n<packages>`r`n</packages>"
        $packagesConfig = Get-Content "$projectDir\packages.config"
    }
    $package = $packagesConfig.CreateElement("package", $packagesConfig.DocumentElement.NamespaceURI)
    $packagesConfig.SelectSingleNode("//packages").AppendChild($package)|out-null
    $package.SetAttribute("id", $packageName)
    $package.SetAttribute("version", $version)
    $package.SetAttribute("targetFramework", "net461")
    $packagesConfig.Save($packagesConfigpath)
    $csproj.Save($projectPath)  
    return $package 
}
function CloneItem{
    [cmdletbinding()]
    param(
        [parameter(ValueFromPipeline=$True,mandatory=$True)]
        [string]$Path,
        [parameter(mandatory=$True)]
        [string] $TargetDir,
        [parameter(mandatory=$True)]
        [string]$SourceDir
    )
    $targetFile = $TargetDir + $Path.SubString($SourceDir.Length);
    
    if (!((Get-Item $Path) -is [System.IO.DirectoryInfo])){
        $dirName=Split-Path $targetFile -Parent
        New-Item -ItemType Directory $dirName -ErrorAction SilentlyContinue
        Copy-Item $Path -destination $targetFile -Force
        Write-Output $targetFile
    }
}
function GetProjects{
    param(
    [Parameter(ValueFromPipeline)]
    $projects)
    
    ($projects.Include -split ";")|ForEach-Object{
            $item=$_.Trim()
            if ($item -ne ""){
               $project="$PSScriptRoot\..\..\$item"
               if ((Get-Item $project).GetType().Name -eq "FileInfo"){
                   $project
                }
            }
        }|Where-Object{$_ -ne "" -and $_ -ne $null}
}

function GetPackageName($include){
    $packageName=$include
    if (!($include -like "DevExpress.XAF.*")){
        if ($include -like "DevExpress.Xtra*"){
            $packageName="DevExpress.Win"
            if ($include -like "*XtraRichEdit*"){
                $packageName="DevExpress.Win.RichEdit"
            }
            elseif ($include -like "*XtraChart*"){
                $packageName=$include.Replace("Xtra","")
                if ($include -like "*Wizard*" -or $include -like "*Extenions*" -or $include -like "*UI*"){
                    $packageName="DevExpress.Win.Charts"
                }
            }
            elseif ($include -like "*XtraGauges*"){
                $packageName=$include.Replace("Xtra","")
                if ($include -like "*Win*"){
                    $packageName="DevExpress.Win.Gauges"
                }
            }
        }
        elseif ($include -like "DevExpress.Web.ASPx*"){
            $packageName="DevExpress.Web"
        }
        elseif ($include -like "DevExpress.Web.Resources*"){
            $packageName="DevExpress.Web"
        }
        elseif ($include -like "DevExpress.Workflow.Activities*"){
            $packageName="DevExpress.ExpressApp.Workflow"
        }
        elseif ($include -like "DevExpress.BonusSkins*"){
            $packageName="DevExpress.Win.BonusSkins"
        }
        elseif ($include -like "DevExpress.Docs*"){
            $packageName="DevExpress.Document.Processor"
        }
        elseif ($include -like "DevExpress.Dashboard.Web*"){
            $packageName="DevExpress.Web.Dashboard"
        }
        elseif ($include -like "DevExpress.Dashboard.Win*"){
            $packageName="DevExpress.Win.Dashboard"
        }    
        if ($include -like "DevExpress.Dashboard*"){
            if ($include -like "*WebForms*"){
                $packageName="DevExpress.Web.Dashboard"
            }
            elseif ($include -like "*Win*"){
                $packageName="DevExpress.Win.Dashboard"
            }
        }    
        if ($include -eq "DevExpress.XtraCharts.Web"){
            $packageName="DevExpress.Web.Visualization"
        }
        if ($include -like "*XtraReport*"){
            $packageName="DevExpress.Reporting.Core"
            if ($include -like "*Extensions*"){
                $packageName="DevExpress.Win.Reporting"
            }
            elseif ($include -like "*WebForms*" ){
                $packageName="DevExpress.Web.Reporting"
            }
            elseif ($include -like "*.Web.*" ){
                $packageName="DevExpress.Web.Reporting.Common"
            }

        }
        if ($include -like "*XtraScheduler*"){
            $packageName="DevExpress.Win.Scheduler"
            if ($include -like "*reporting*"){
                $packageName="DevExpress.Win.SchedulerReporting"
            }
            elseif ($include -like "*Core*"){
                $packageName="DevExpress.Scheduler.Core"
            }
        }
        if ($include -like "*ASPxScheduler*"){
            $packageName="DevExpress.Web.Scheduler"
        }
        if ($include -like "*ASPxThemes*"){
            $packageName="DevExpress.Web.Themes"
        }
    }
    $packageName
}
