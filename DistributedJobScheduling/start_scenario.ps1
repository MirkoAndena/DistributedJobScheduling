$nodes = 10
$initiatorIndex = 0 #No Initiator is negative
if ($args.Count -gt 0) {
    $nodes = $args[0]
    $initiatorIndex = $args[1]
}

Write-Host 'Creation of' $nodes 'machines'


$nodeContainers = New-Object string[] $nodes
For ($i=0; $i -lt $nodeContainers.Count; $i++) {
    $nodeDirectory = $(Get-Location).tostring() + "/AppDataDocker/node_" + $i
    $toExecute = 'docker run --mount src="' + $nodeDirectory + '",target=/app/AppData,type=bind --restart=on-failure -d distributedjobscheduling:latest ' + $i
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