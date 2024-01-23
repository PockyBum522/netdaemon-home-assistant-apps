Set-Location "D:\source\repos\PockyBum522 Github\netdaemon-home-assistant-apps\src\AllenStreetNetDaemonApps"

# Update the codegen
dotnet tool update -g NetDaemon.HassModel.CodeGen

dotnet tool run nd-codegen

Set-Location "D:\source\repos\PockyBum522 Github\netdaemon-home-assistant-apps\src\Scripts"
