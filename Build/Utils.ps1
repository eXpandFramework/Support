function Clone-Item{
    [cmdletbinding()]
    param(
        [parameter(ValueFromPipeline=$True,mandatory=$True)]
        [string]$Path,
        [parameter(mandatory=$True)]
        [string] $TargetDir,
        [parameter(mandatory=$True)]
        [string]$SourceDir
    )
    $targetFile = $TargetDir + $Path.SubString($SourceDir.Length);
    
    if (!((Get-Item $Path) -is [System.IO.DirectoryInfo])){
        $dirName=Split-Path $targetFile -Parent
        New-Item -ItemType Directory $dirName -ErrorAction SilentlyContinue
        Copy-Item $Path -destination $targetFile -Force
        Write-Output $targetFile
    }
}

function ZipFiles( $zipfilename, $sourcedir ){
   Add-Type -Assembly System.IO.Compression.FileSystem
   $compressionLevel = [System.IO.Compression.CompressionLevel]::Optimal
   [System.IO.Compression.ZipFile]::CreateFromDirectory($sourcedir,
        $zipfilename, $compressionLevel, $false)
}