param(
    [string]$rootPath = $(get-item "$PSScriptRoot\..\..").FullName,
    [string[]]$packageSources = @("https://api.nuget.org/v3/index.json", "https://xpandnugetserver.azurewebsites.net/nuget", "C:\Program Files (x86)\DevExpress 18.2\Components\System\Components\packages"),
    [string]$version = "18.2.401.0"
)
$ErrorActionPreference = "Stop"
& "$PSScriptRoot\ImportXpandPosh.ps1"
& "$PSScriptRoot\InstallDX.ps1" "$PSScriptRoot\..\..\Xpand.dll" "$PSScriptRoot\..\Tool\NuGet.exe" $packageSources $rootPath

Get-ChildItem $rootPath "*.csproj" -Recurse|ForEach-Object {
    $projectPath = $_.FullName
    Write-Host "Checking DX references $projectPath"
    
    $projectDir = (Get-Item $projectPath).DirectoryName
    
    [xml]$csproj = Get-Content $projectPath
    
    $references = $csproj.Project.ItemGroup.Reference|Where-Object {"$($_.Include)".StartsWith("DevExpress")}|
        Where-Object {!"$($_.Include)".Contains(".DXCore.")}
    
    $references|ForEach-Object {
        $reference = $_
        $v = New-Object System.Version $version
        $version = "$($v.Major).$($v.Minor).$($v.Build.ToString().Substring(0,1))"
        $outputPath = "$rootPath\Xpand.Dll"
            
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

