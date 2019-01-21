param(
    $binPath = "$PSScriptRoot\..\..\bin", 
    $nugetExe = "$PSScriptRoot\..\NuGet.exe", 
    $dxSource = 'C:\Program Files (x86)\DevExpress 18.2\Components\System\Components\packages',
    $sourcePath = $null,
    $installDXUri = "https://gist.githubusercontent.com/apobekiaris/68e6a22450aa6eacce2e3535a6963a06/raw/5b30a89f0b8e2d4e7814aab16847abf6f7fe6fea/InstallDX.ps1",
    $collectDxNugetsUri = "https://gist.githubusercontent.com/apobekiaris/d3d306fa6b28a01e83f1289047a49308/raw/a92c608e175077af8d68a590f66f6879fe73dc0b/CollectDXNugets.ps1"
)
$webclient = New-Object System.Net.WebClient
$fileName = "$([System.io.path]::GetTempPath())\CollectDXNugets.ps1"
Set-Content $fileName $webclient.DownloadString($collectDxNugetsUri)
$fileName = "$([System.io.path]::GetTempPath())\InstallDX.ps1"
Set-Content $fileName $webclient.DownloadString($installDXUri)
& $fileName $binPath $nugetExe $dxSource $sourcePath 
$webclient.Dispose()
