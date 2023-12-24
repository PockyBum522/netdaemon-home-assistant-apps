# PowerShell Script to build a .NET project, clean a directory, and move build output

# Define the paths
$solutionPath = "D:\source\repos\PockyBum522 Github\netdaemon-home-assistant-apps\src\AllenStreetNetDaemonApps"
$solutionFilename = "AllenStreetNetDaemonApps.csproj"

$buildPath = "\\192.168.1.25\config\netdaemon3\"

# Step 1: Run dotnet build
Set-Location $solutionPath

# Step 2: Delete everything in the network share except 'logs' folder
Write-Host "Cleaning network share..."
Get-ChildItem -Path $buildPath -Exclude "logs" | ForEach-Object {
    if ($_.PSIsContainer -and $_.Name -ne "logs") {
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    } elseif (!$_.PSIsContainer) {
        Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $_.FullName) {
        Write-Warning "Failed to delete item: $($_.FullName)"
    }
}

& dotnet build $solutionPath\$solutionFilename --output $buildPath

# End of the script
Write-Host "Script execution completed."