param(
    [string]$configuration="Release",
    [string]$version=$null,
    [string]$msbuild=$null,
    [string[]]$packageSources=@("https://api.nuget.org/v3/index.json","https://xpandnugetserver.azurewebsites.net/nuget","C:\Program Files (x86)\DevExpress 18.2\Components\System\Components\packages")   ,
    [string[]]$msbuildArgs=@("/p:Configuration=$configuration","/WarnAsError","/v:m"),
    [int]$throttle=(Get-WmiObject -class Win32_ComputerSystem).numberoflogicalprocessors,
    [string[]]$taskList=@("Release"),
    [string]$publishNugetFeed="https://api.nuget.org/v3/index.json",
    [string]$nugetApiKey=$null,
    [switch]$UseAllPackageSources
)

$(@{
    Name = "psake"
    Version ="4.7.4"
}),$(@{
    Name = "XpandPosh"
    Version ="1.0.12"
}),$(@{
    Name = "PoshRSJob"
    Version ="1.7.4.4"
})|ForEach-Object{
    & "$PSScriptRoot\Install-Module.ps1" $_
} 

if (!$version){
    $version=Get-XVersionFromFile "$PSScriptRoot\..\..\Xpand\Xpand.Utils\Properties\XpandAssemblyInfo.cs"
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
    "publishNugetFeed"=$publishNugetFeed;
    "nugetApiKey"=$nugetApiKey;
    "UseAllPackageSources"=$UseAllPackageSources
} -taskList $taskList
