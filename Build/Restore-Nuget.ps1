param(
    [string[]]$packageSources=@("https://api.nuget.org/v3/index.json","https://xpandnugetserver.azurewebsites.net/nuget","C:\Program Files (x86)\DevExpress 18.2\Components\System\Components\packages") ,  
    [int]$throttle=10,
    [string]$version="18.2.300.1"
)

[xml]$xml =Get-Content "$PSScriptRoot\Xpand.projects"
$group=$xml.Project.ItemGroup
Write-Host "Starting nuget restore from $currentLocation\Restore-Nuget.ps1...." -f "Blue"

$rootPath="$PSScriptRoot\..\.."
Update-XHintPath -OutputPath "$rootPath\Xpand.Dll" -SourcesPath $rootPath
get-childitem $rootPath "packages.config" -Recurse|ForEach-Object{
    $xml=Get-Content $_.FullName 
    $xml.packages.Package.Id|Group-Object |Where-Object{$_.Count -gt 1}|ForEach-Object{
        $_.Group|Select-Object -skip 1 | ForEach-Object{
            $id=$_
            $project=$xml.packages.Package|Where-Object{$_.id -eq $id}|select -first 1
            $project.parentNode.RemoveChild($project)
        }
    }
    $xml.Save($_.FullName)
}

. $PSScriptRoot\Utils.ps1
$projects=($group.DemoSolutions|GetProjects)+
($group.DemoTesterProjects|GetProjects)+
($group.ModuleProjects|GetProjects)+
($group.HelperProjects|GetProjects)+
($group.VSAddons|GetProjects)+
($group.EasyTestProjects|GetProjects)+
($group.CoreProjects|GetProjects)


$psObj=[PSCustomObject]@{
    PackagesDirectory = (Get-Item "$PSScriptRoot\..\_third_party_assemblies\Packages").FullName
    packageSources=[system.string]::join(";",$packageSources)
    projects=$projects
} 

workflow Restore-Nuget{
    param([PSCustomObject]$psObj)
    $complete = 0
    foreach -parallel ($project in $psObj.Projects) {
        InlineScript{
            & nuget Restore $Using:project -PackagesDirectory $Using:psObj.PackagesDirectory -source $Using:psObj.packageSources
        }
        $Workflow:complete = $Workflow:complete + 1 
        [int]$percentComplete = ($Workflow:complete * 100) / $Workflow:psObj.Projects.Count
        Write-Progress -Id 1 -Activity  "Restoring Nugets" -PercentComplete $percentComplete -Status "$percentComplete% :$($project)"
    }
    Write-Progress -Id 1 -Status "Ready" -Activity "Restoring Nugets" -Completed
}

Restore-Nuget $psObj
