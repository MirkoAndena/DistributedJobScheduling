$initiatorContainer = docker run -d distributed-job-scheduling 0 coordinator
$nodeContainers = New-Object string[] 10
For ($i=0; $i -lt $nodeContainers.Count; $i++) {
    $nodeContainers[$i] = docker run -d distributed-job-scheduling $i
}

Write-Host -NoNewLine 'Press any key to kill...';
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');

docker container stop $initiatorContainer
docker container rm $initiatorContainer
Foreach ($container in $nodeContainers)
{
    docker container stop $container
    docker container rm $container
}