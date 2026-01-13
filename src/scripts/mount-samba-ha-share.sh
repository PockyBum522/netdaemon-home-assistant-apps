# Script to build a .NET project, clean a directory, and move build output

# WARNING, MUCH IN THIS FILE WILL NEED TO BE CONFIGURED FOR YOUR PATHS, SETTINGS, ETC...

# READ COMMENTS THROUGHOUT SCRIPT CAREFULLY

# Local paths on your computer:
solution_path="/media/secondary/repos/netdaemon-home-assistant-apps/src/AllenStreetNetDaemonApps"
solution_filename="AllenStreetNetDaemonApps.csproj"
build_path_local="$solution_path/bin/Debug/net9.0"

# Samba temp mount point
mount_point="/tmp/ha_smb"

# ND folder after SSH commands below SSH into the server
build_path_remote="/root/config"


# Import our secrets
source /media/secondary/repos/netdaemon-home-assistant-apps/src/scripts/SECRETS.env

mkdir -p "${mount_point}"

sudo chown david "${mount_point}"

# Note: You may need 'sudo' here if mounting requires root access
sudo mount -t cifs "//192.168.1.25/config/netdaemon5" /tmp/ha_smb -o username="${SAMBA_USER}",password="${SAMBA_PASS}",rw
  
echo "Script execution completed."
echo ""

# echo "Open logs folder? Press Y to open, any other key to not: " -NoNewLine

# $key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');

# if ($key.Character -eq 'y')
# {	
# 	explorer "\\192.168.1.25\config\netdaemon4\logs"
# }

cd "$solution_path/../scripts"

nemo "$mount_point"

