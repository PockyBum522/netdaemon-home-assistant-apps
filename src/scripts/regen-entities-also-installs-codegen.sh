# Add check to see if it needs to be installed, here


dotnet tool update -g NetDaemon.HassModel.CodeGen

cd ../AllenStreetNetDaemonApps


# Add check to see if it needs to be restored, here


dotnet tool run nd-codegen

