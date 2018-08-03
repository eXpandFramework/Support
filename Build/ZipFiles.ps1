Function Zip-Files {
    [cmdletbinding()]
    Param (
        [parameter(ValueFromPipeline=$True)]
        [string]$fileName,
        [string]$dir,
        [System.IO.Compression.CompressionLevel]$compressionLevel = [System.IO.Compression.CompressionLevel]::Optimal,
        [bool]$deleteAfterArchiving=$True
    )
    Begin {
        Write-Verbose "Initialize stuff in Begin block"
        if (!$fileName){
            $currentLocation = Get-Location
            $fileName = (Get-Location | Split-Path -Leaf) +'.zip'
            $fileName=[System.IO.Path]::Combine($currentLocation,$fileName)
        }
    }

    Process {


        Add-Type -Assembly System.IO.Compression.FileSystem
        $tempDir = [System.IO.Path]::GetTempPath()
        $targetDir=$dir
        if ($dir -eq ""){
            $targetDir=Split-Path $fileName
        }
        $fileNameWithoutPath=[io.path]::GetFileName($fileName)
        $tempFileName = [io.path]::Combine($tempDir,$fileNameWithoutPath)
        Write-Verbose "Zipping $fileName into $tempDir"
        [System.IO.Compression.ZipFile]::CreateFromDirectory($targetDir,$tempFilename, $compressionLevel, $false)
        Copy-Item -Force -Path $tempFileName -Destination $fileName
        Remove-Item -Force -Path $tempFileName
        if ($deleteAfterArchiving){
            Get-ChildItem -Path $targetDir -Exclude $fileNameWithoutPath -Recurse | Select -ExpandProperty FullName | Remove-Item -Force -Recurse
        }
    }

    End {
        Write-Verbose "Final work in End block"
    }
}