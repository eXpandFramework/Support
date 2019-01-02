param(
    [string]$configuration="Release",
    [string]$version=$null,
    [string]$msbuild=$null,
    [string[]]$packageSources=@("https://api.nuget.org/v3/index.json","https://xpandnugetserver.azurewebsites.net/nuget","C:\Program Files (x86)\DevExpress 18.2\Components\System\Components\packages")   ,
    [string[]]$msbuildArgs=@("/p:Configuration=$configuration","/WarnAsError","/v:m"),
    [int]$throttle=(Get-WmiObject -class Win32_ComputerSystem).numberoflogicalprocessors,
    [string[]]$taskList=@("Release")
)
Get-PackageProvider -Name "Nuget" -Force 


Import-Module "$PSScriptRoot\XpandPosh.psm1" -Force 
if (!$version){
    $version=Get-VersionFromFile "$PSScriptRoot\..\..\Xpand\Xpand.Utils\Properties\XpandAssemblyInfo.cs"
}
if (!$msbuild){
    $msbuild=Get-MsBuildLocation
}
Set-PSRepository -Name "PSGallery" -InstallationPolicy Trusted
if (!(Get-module -ListAvailable -Name PoshRSJob)){
    Install-Module -Name PoshRSJob 
}
if (!(Get-Module -ListAvailable -Name psake)){
    Install-Module -Name psake
}

Write-HostHashTable $(Get-AllParameters $MyInvocation $(Get-Variable))
$clean=$($taskList -in "Release")
Invoke-psake  "$PSScriptRoot\Build.ps1" -properties @{
    "version"=$version;
    "msbuild"=$msbuild;
    "clean"=$clean;
    "msbuildArgs"=$msbuildArgs;
    "throttle"=$throttle;
    "packageSources"=$packageSources;
} -taskList $taskList
