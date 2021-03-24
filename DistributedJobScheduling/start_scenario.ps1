$nodes = 10
if ($args.Count -gt 0) {
    $nodes = $args[0]
}

Write-Host 'Creation of' $nodes 'machines'

$initiatorIndex = 0 #No Initiator is negative
$nodeContainers = New-Object string[] $nodes
For ($i=0; $i -lt $nodeContainers.Count; $i++) {
    $nodeDirectory = $(Get-Location).tostring() + "/ExecutorsStorage/node_" + $i
    $toExecute = 'docker run --mount src="' + $nodeDirectory + '",target=/app/DataStore,type=bind --restart=on-failure -d distributed-job-scheduling ' + $i
    if ($i -eq $initiatorIndex) {
        $toExecute += " coordinator"
    }
    $nodeContainers[$i] = Invoke-Expression($toExecute)
}

Write-Host -NoNewLine 'Press any key to kill...';
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');

Foreach ($container in $nodeContainers)
{
    docker container stop $container
    docker container rm $container
}