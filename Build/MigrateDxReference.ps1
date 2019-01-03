param(
    [string]$rootPath = $(get-item "$PSScriptRoot\..\..").FullName,
    [string[]]$packageSources = @("https://api.nuget.org/v3/index.json", "https://xpandnugetserver.azurewebsites.net/nuget", "C:\Program Files (x86)\DevExpress 18.2\Components\System\Components\packages"),
    [string]$version = "18.2.401.0"
)
$ErrorActionPreference = "Stop"
import-module "$PSScriptRoot\XpandPosh.psm1" -Force
. "$PSScriptRoot\Utils.ps1"
Get-ChildItem $rootPath "*.csproj" -Recurse|ForEach-Object {
    $projectPath = $_.FullName
    Write-Host "Checking DX references $projectPath"
    
    $projectDir = (Get-Item $projectPath).DirectoryName
    
    [xml]$csproj = Get-Content $projectPath
    $packagesDir = "$rootPath\Support\_third_party_assemblies\Packages"
    $packagesConfigPath = "$projectDir\packages.config"
    
    $references = $csproj.Project.ItemGroup.Reference|Where-Object {"$($_.Include)".StartsWith("DevExpress")}|
        Where-Object {!"$($_.Include)".Contains(".DXCore.")}
    
    $references|ForEach-Object {
        $reference = $_
        $include = $reference.Include -creplace '(\.v[\d]{2}\.[\d]{1})', ''
        $packageName = GetPackageName $include
        if ($packageName) {
            $v = New-Object System.Version $version
            $sources = [System.String]::Join(";", $packageSources)
            $version = "$($v.Major).$($v.Minor).$($v.Build.ToString().Substring(0,1))"
            $outputPath = "$packagesDir\$packageName.$version\lib\net452"
            
            if (InstallPackage $outputPath $packageName $sources $rootPath $version $csproj $packagesConfigPath $projectDir $projectPath) {
                Write-Host "Updating package $include" -f "Cyan"
                if (!$reference.Hintpath) {
                    $reference.AppendChild($reference.OwnerDocument.CreateElement("HintPath", $csproj.DocumentElement.NamespaceURI))|out-null
                }            
                $hintPath = Get-RelativePath $projectPath $outputPath
                $reference.HintPath = "$hintPath\$($reference.Include).dll"
                if (!$(Test-path $("$projectDir\$hintPath"))) {
                    throw "File not found $($reference.HintPath)"
                }
                $csproj.Save($projectPath)      
            }
        }
    }

}

