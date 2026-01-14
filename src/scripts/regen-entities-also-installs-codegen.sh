# Add check to see if it needs to be installed, here


/home/david/.dotnet/dotnet tool update -g NetDaemon.HassModel.CodeGen

cd ../AllenStreetNetDaemonApps


# Add check to see if it needs to be restored, here


/home/david/.dotnet/dotnet tool run nd-codegen

