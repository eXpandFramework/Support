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
