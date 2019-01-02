param(
    [string[]]$packageSources=@("https://api.nuget.org/v3/index.json","https://xpandnugetserver.azurewebsites.net/nuget","C:\Program Files (x86)\DevExpress 18.2\Components\System\Components\packages") ,  
    [int]$throttle=10,
    [string]$version="18.2.300.1"
)
$nugetExe=$PSScriptRoot+"\..\Tool\nuget.exe"

[xml]$xml =Get-Content "$PSScriptRoot\Xpand.projects"
$group=$xml.Project.ItemGroup
Write-Host "Starting nuget restore from $currentLocation\Restore-Nuget.ps1...." -f "Blue"
$paramObject = [pscustomobject] @{
    location = $PSScriptRoot
    nugetExe=$PSScriptRoot+"\..\Tool\nuget.exe"
    packageSources=[system.string]::join(";",$packageSources)
}

& "$PSScriptRoot\MigrateDxReference.ps1" -version $version -packageSources $packageSources
get-childitem "$PSScriptRoot\..\.." "packages.config" -Recurse|ForEach-Object{
    # $content=Get-Content $_.FullName -Raw
    # $content=$content.Replace("Id=","id=").Replace("Version=","version=").Replace("TargetFramework=","targetFramework=").Replace("<Package","<package")
    # Set-Content $_.FullName $content
    
    $xml=Get-Content $_.FullName 
    $xml.packages.Package.Id|Group-Object |where{$_.Count -gt 1}|ForEach-Object{
        $_.Group|select -skip 1 | ForEach-Object{
            $id=$_
            $item=$xml.packages.Package|where{$_.id -eq $id}|select -first 1
            $item.parentNode.RemoveChild($item)
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


$sb={
    param($parameter)
    Push-Location $parameter.location
    $packagesDirectory= "$($parameter.location)\..\_third_party_assemblies\Packages"
    $params="restore ""$_""  -PackagesDirectory $packagesDirectory -source ""$($parameter.packageSources)"""
    $result=New-Command $_ $parameter.nugetExe $params $parameter.location
    [PSCustomObject]@{
        result = $result
        project=$_
        params=$params
    } 
}
Import-Module "$PSScriptRoot\XpandPosh.psm1" -Force 
$modules=(Get-Module XpandPosh).Path

$projects|start-rsjob  $sb -argumentlist $paramObject -Throttle $throttle -ModulesToImport $modules |Wait-RSJob -ShowProgress -Timeout 180 |ForEach-Object{
    $j=Get-RSJob $_ |Receive-RSJob
    $j.result.stdout
    $j.result.project
    $j.params
    if ($j.result.stderr){
        throw "Fail to restore $j`n`r$($j.result.stderr)" 
    }
    elseif ($j.result.stdout -like "*invalid arguments*") {
        throw "Invalid arguments, params:$($j.params)"
    }
    else{
        Write-Host "Packages restored succefully for $($j.result.commandTitle)" -f "Green"
    }
}
Get-RsJob -State Running