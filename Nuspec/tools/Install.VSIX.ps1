param($project)
#. "VSInstaller.ps1"
while (Get-Process  devenv -ErrorAction SilentlyContinue) {
    Write-Host "Close all VS instances and hit any key"     
    $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyUp") > $null
}

& "uninstall.VSIX.ps1" -project $project
Write-Host "Trying to install..."
$vsxInstaller = "VSIXBootstrapper.exe"
$vsx = "/q Xpand.VSIX-17.1.6.4.vsix"
$info =New-Object -TypeName System.Diagnostics.ProcessStartInfo -ArgumentList $vsxInstaller,$vsx
$info.WorkingDirectory=$PSScriptRoot
$p=[System.Diagnostics.Process]::Start($info)
$p.WaitForExit()

if ($p.ExitCode -ne 0){
    $exitReason=Show-ExitReason $p.ExitCode
    write=$Host $exitReason
}
else {
    Write-Host "Xpand.VSIX installed. "
}
Write-Host "Press a key to continue."
$Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyUp") > $null