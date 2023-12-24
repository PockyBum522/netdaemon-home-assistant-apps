# PowerShell Script to build a .NET project, clean a directory, and move build output

# Define the paths
$solutionPath = "D:\source\repos\PockyBum522 Github\netdaemon-home-assistant-apps\src\AllenStreetNetDaemonApps"
$solutionFilename = "AllenStreetNetDaemonApps.csproj"

$buildPath = "\\192.168.1.25\config\netdaemon3\"

# Step 1: Run dotnet build
Set-Location $solutionPath
& dotnet build $solutionPath\$solutionFilename --output $buildPath

# Step 2: Delete everything in the network share except 'logs' folder
# Write-Host "Cleaning network share..."
# Get-ChildItem -Path $networkShare -Exclude "logs" | ForEach-Object {
    # if ($_.PSIsContainer -and $_.Name -ne "logs") {
        # Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    # } elseif (!$_.PSIsContainer) {
        # Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
    # }
    # if (Test-Path $_.FullName) {
        # Write-Warning "Failed to delete item: $($_.FullName)"
    # }
# }

# # Step 3: Move the build output
# Write-Host "Moving build output to network share..."
# Move-Item -Path $buildOutputPath\* -Destination $networkShare -Force

# # Step 4: Set location to the network share
# Set-Location -Path $networkShare
# Write-Host "Current directory set to: $(Get-Location)"

# # End of the script
# Write-Host "Script execution completed."