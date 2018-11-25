function UpdateNode($containerSpec,$specs,$nuspecFolder,$nodeName){
    $containerPath="$nuspecFolder/$containerSpec.nuspec"
    [xml]$container=Get-Content $containerPath
    $ns = New-Object System.Xml.XmlNamespaceManager($container.NameTable)
    $ns.AddNamespace("ns", $container.DocumentElement.NamespaceURI)
    $noeToUpdate=$container.SelectSingleNode("//ns:$nodeName",$ns)
    $noeToUpdate.RemoveAll()
    $specs|foreach {
        [xml] $info = Get-Content "$nuspecFolder\$_"
        $ns = New-Object System.Xml.XmlNamespaceManager($info.NameTable)
        $ns.AddNamespace("ns", $info.DocumentElement.NamespaceURI)

        $info.SelectSingleNode("//ns:$nodeName",$ns).ChildNodes |Select-Object -ExcludeProperty xmlns |foreach{$_.outerxml}
    }|Sort-object|Get-Unique|foreach{
        $temp=[xml]$_
        $node=$container.ImportNode($temp.FirstChild,$true)
        $node.RemoveAttribute("xmlns")
        write-host $node.outerxml
        $noeToUpdate.AppendChild($node)
    }
    $container.Save($containerPath)
}
$nuspecFolder="$PSScriptRoot\..\Nuspecs\"
$winSpecs=Get-ChildItem $nuspecFolder  -Filter "*.Win.nuspec"|Select-Object -ExpandProperty Name
$webSpecs=Get-ChildItem $nuspecFolder  -Filter "*.Web.nuspec"|Select-Object -ExpandProperty Name
$agnosticNuspec=Get-ChildItem $nuspecFolder -Filter "*.nuspec" |
    Select-Object -ExpandProperty Name|Select-String -Pattern ($winSpecs+$webSpecs) -SimpleMatch  -NotMatch|
        where {"$_".StartsWith("All_") -eq $false}


        
"dependencies","files"|foreach {UpdateNode "All_Web" $webSpecs $nuspecFolder $_}
"dependencies","files"|foreach {UpdateNode "All_Win" $winSpecs $nuspecFolder $_}
"dependencies","files"|foreach {UpdateNode "All_Agnostic" $agnosticNuspec $nuspecFolder $_}


