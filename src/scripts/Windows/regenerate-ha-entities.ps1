Set-Location "D:\repos\netdaemon-home-assistant-apps\src\AllenStreetNetDaemonApps"

# Update the codegen
dotnet tool update -g NetDaemon.HassModel.CodeGen

#dotnet tool restore

nd-codegen

Set-Location "D:\repos\netdaemon-home-assistant-apps\src\Scripts"
