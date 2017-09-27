param($installPath, $toolsPath, $package, $project)
write-host UNISTALL
$name=$project.FullName
$sb={
    cmd /c start powershell -Command " & '$toolsPath\UnInstall.VSIX.ps1' '$name' 'true'"
}
Invoke-Command -ScriptBlock $sb    

