param($installPath, $toolsPath, $package, $project)
write-host "INSTALL"
$name=$project.FullName
$sb={
    cmd /c start powershell -Command " & '$toolsPath\Install.VSIX.ps1' '$name'"
}
Invoke-Command -ScriptBlock $sb 