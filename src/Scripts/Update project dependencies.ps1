Set-Location "D:\repos\netdaemon-home-assistant-apps\src\AllenStreetNetDaemonApps"

# Update all nugets to latest versions
$regex = 'PackageReference Include="([^"]*)" Version="([^"]*)"'

ForEach ($file in Get-ChildItem . -Recurse | Where-Object { $_.Extension -like "*proj" }) {
    $packages = Get-Content $file.FullName |
    Select-String -Pattern $regex -AllMatches | 
        ForEach-Object { $_.Matches } | 
        ForEach-Object { $_.Groups[1].Value.ToString() } | 
        Sort-Object -Unique
    
    ForEach ($package in $packages) {
        Write-Host "Update $file package :$package"  -ForegroundColor Magenta
        $fullName = $file.FullName
        
        & dotnet add $fullName package $package
    }
}

Set-Location "D:\repos\netdaemon-home-assistant-apps\src\Scripts"
