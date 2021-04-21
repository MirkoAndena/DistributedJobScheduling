$nodeDirectory = $(Get-Location).tostring() + "/AppDataDocker/client"
$toExecute = 'docker run --mount src="' + $nodeDirectory + '",target=/app/AppDataClient,type=bind --sig-proxy=false -d distributedjobscheduling:latest client ' + $args
$clientContainer = Invoke-Expression($toExecute)

docker attach $clientContainer

Write-Host -NoNewLine 'Press any key to kill...';
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');

docker container stop $clientContainer
docker container rm $clientContainer