# PowerShell Script to build a .NET project, clean a directory, and move build output

# Define the paths
$solutionPath = "D:\source\repos\PockyBum522 Github\netdaemon-home-assistant-apps\src\AllenStreetNetDaemonApps"
$solutionFilename = "AllenStreetNetDaemonApps.csproj"

$buildPath = "\\192.168.1.25\config\netdaemon4"

# Step 1: Run dotnet build
Set-Location $solutionPath

New-Item -Path $buildPath\apps -ItemType Directory -Force

# Step 2: Delete everything in the network share except 'logs' folder
Write-Host "Cleaning network share..."
Get-ChildItem -Path $buildPath -Exclude "logs","apps" | ForEach-Object {
    if ($_.PSIsContainer -and $_.Name -ne "logs" -and $_.Name -ne "apps") {
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    } elseif (!$_.PSIsContainer) {
        Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $_.FullName) {
        Write-Warning "Failed to delete item: $($_.FullName)"
    }
}

& dotnet build $solutionPath\$solutionFilename --output $buildPath

Write-Host "Built and deployed, now restarting ND addon"

ssh pockybum522-ha-ssh@192.168.1.25 -i D:\keys\DAVID-LAPTOP-2023-12-24_TO_HOME_ASSISTANT_SSH_ADDON -p 3248 'sudo docker restart addon_c6a2317c_netdaemon4'

# End of the script
Write-Host "ND addon restarted - Script execution completed."


Write-Host "Open logs folder? Press Y to open, any other key to not: " -NoNewLine

$key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');

if ($key.Character -eq 'y')
{	
	explorer "\\192.168.1.25\config\netdaemon4\logs"
}

