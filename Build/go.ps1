param(
    [string]$configuration="Release",
    [string]$version=$null,
    [string]$msbuild=$null,
    [string[]]$packageSources=@("https://api.nuget.org/v3/index.json","https://xpandnugetserver.azurewebsites.net/nuget","C:\Program Files (x86)\DevExpress 18.2\Components\System\Components\packages")   ,
    [string[]]$msbuildArgs=@("/p:Configuration=$configuration","/WarnAsError","/v:m"),
    [string[]]$taskList=@("Release"),
    [string]$nugetApiKey=$null,
    [switch]$UseAllPackageSources,
    [string]$Repository="eXpand"
)

$(@{
    Name = "psake"
    Version ="4.7.4"
}),$(@{
    Name = "XpandPosh"
    Version ="1.3.6"
})|ForEach-Object{
    & "$PSScriptRoot\Install-Module.ps1" $_
} 
if (!$version){
    $nextVersion=Get-XXpandVersion -Next
    Write-host "NextVersion=$nextVersion"
    return $nextVersion
}

if (!$msbuild){
    $msbuild=Get-XMsBuildLocation
}

$clean=$($taskList -in "Release")
Invoke-Xpsake  "$PSScriptRoot\Build.ps1" -properties @{
    "version"=$version;
    "msbuild"=$msbuild;
    "clean"=$clean;
    "msbuildArgs"=$msbuildArgs;
    "throttle"=$throttle;
    "packageSources"=$packageSources;
    "nugetApiKey"=$nugetApiKey;
    "Repository"=$Repository;
    "UseAllPackageSources"=$UseAllPackageSources
} -taskList $taskList
