param($project,$config)
. "VSInstaller.ps1"
while (Get-Process  devenv -ErrorAction SilentlyContinue) {
    Write-Host "Close all VS instances and hit any key"     
    $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyUp") > $null
}
Write-Host "Trying to uninstall..."
$vsxInstaller = "VSIXBootstrapper.exe"
$vsx = "/q /u:""Xpand.VSIX.Apostolis Bekiaris.4ab62fb3-4108-4b4d-9f45-8a265487d3dc"""
$info =New-Object -TypeName System.Diagnostics.ProcessStartInfo -ArgumentList $vsxInstaller,$vsx
$info.WorkingDirectory=$PSScriptRoot
$p=[System.Diagnostics.Process]::Start($info)
$p.WaitForExit()
if ($p.ExitCode -ne 0){
    $exitReason=Show-ExitReason $p.ExitCode
    Write-Host $exitReason
}
else {
    if ($config -eq "true"){
        $projectXmlFile=([xml](Get-Content $project))
        $ns = New-Object System.Xml.XmlNamespaceManager($projectXmlFile.NameTable)
        $ns.AddNamespace("ns", $projectXmlFile.DocumentElement.NamespaceURI)
        if ($licElement){
            $licElement=$projectXmlFile.selectNodes("//ns:Content",$ns)|where {$_.include -eq "License.txt"}|select -first 1 
            $licElement.ParentNode.removeChild($licElement)
            $projectXmlFile.Save($project)
        }
        
        $projectDir=[System.IO.Path]::GetDirectoryName($project)
        $packagesXmlFile=([xml](Get-Content "$projectDir\packages.config"))
        $element=$packagesXmlFile.SelectNodes("//package")|where {$_.id -eq "eXpandVSIX"}|select  -First 1
        if ($element){
            $element.ParentNode.removeChild($element)
            $packagesXmlFile.Save("$projectDir\packages.config")
        }
    }
    Write-Host "Xpand.VSIX uninstalled"
}
if ($config -eq "true"){
    Write-Host "Press a key to continue."
    $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyUp") > $null
}
