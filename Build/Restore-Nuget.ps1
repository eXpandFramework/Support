. "$PSScriptRoot\Invoke-InParallel.ps1"

$currentLocation=Get-Location

$xml = [xml](Get-Content "$currentLocation\Support\Build\Xpand.projects")
$ns = new-object Xml.XmlNamespaceManager $xml.NameTable
$ns.AddNamespace("msb", "http://schemas.microsoft.com/developer/msbuild/2003")
"CoreProjects","EasyTestProjects","HelperProjects","VSAddons","ModuleProjects","DemoSolutions","DemoTesterProjects" | ForEach-Object{
    $nodes=$xml.SelectNodes("//msb:$_",$ns)
    $projects+=(($nodes | Select-Object -First 1).Attributes["Include"].Value -split ";")|ForEach-Object{
        $path=$_.Trim()
        "$currentLocation\$path"
    }
}

$paramObject = [pscustomobject] @{
    location = (Get-Location)
    nugetExe=$PSScriptRoot+"\..\Tool\nuget.exe"
}
Write-Host Starting nuget restore from $currentLocation\Restore-Nuget.ps1....
Invoke-InParallel -InputObject $projects -Parameter $paramObject -runspaceTimeout 30 -ScriptBlock {  
        Push-Location $parameter.location
        $nugetPath=$parameter.nugetExe
        $sb= "cmd /c $nugetPath restore $_"
        $expr=Invoke-Expression $sb
        Write-Host "$_::::$expr"
    }
    