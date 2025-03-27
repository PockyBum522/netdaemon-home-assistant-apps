#!/usr/bin/bash

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
nd_folder="/config/netdaemon5"
build_path_remote="/root$nd_folder"


# Step 1: Run dotnet build
cd "$solution_path"

# Build the local project
dotnet build "$solution_path/$solution_filename"


# Step 2: Delete everything in the network share except 'logs' folder
ssh pockybum522-ha-ssh@192.168.1.25 -i /media/secondary/keys/DAVID-DESKTOP-2024-10-03 << 'EOF'
    cd "/root/config/netdaemon5"    # CHANGE THE PATH FOR THE CD IN HERE IF IT IS NOT YOUR ND PATH ON THE HA SERVER AFTER SSHING IN
    find . -not -name 'logs' -delete

    # Don't know why, but ND *really* wants this folder to exist even though we're only working with binaries
    mkdir -p "apps"
EOF


# Unmount in case it's still mounted from a prior run of this script
#echo "[LOCAL] Unmounting ..."
#cd - >/dev/null
#sudo umount "${mount_point}"
#rmdir "${mount_point}"




# Step 3: Copy local project over to where it should be on HA
echo "Copying all files in '$build_path_local' to HA ..."
echo ""

# Import our secrets
source /media/secondary/repos/netdaemon-home-assistant-apps/src/scripts/SECRETS.env

mkdir -p "${mount_point}"

sudo chown david "${mount_point}"

# Note: You may need 'sudo' here if mounting requires root access
sudo mount -t cifs "//192.168.1.25/$nd_folder" /tmp/ha_smb -o username="${SAMBA_USER}",password="${SAMBA_PASS}",rw
  
# 3) Copy local build artifacts into the mounted share
echo "[LOCAL] Copying artifacts from '${build_path_local}' to the Samba share ..."
sudo cp -r "${build_path_local}/." "${mount_point}/"




# Step 4: Restart ND addon
echo "Built and deployed, now restarting ND addon"

# You will unfortunately need to turn off protected mode on the "terminal & ssh” addon for this line to work:
# Replace options in below line with your correct user, host, private key path, port, and addon ID
ssh pockybum522-ha-ssh@192.168.1.25 -i /media/secondary/keys/DAVID-DESKTOP-2024-10-03 << 'EOF'
    sudo docker restart addon_c6a2317c_netdaemon5
EOF

echo "ND addon restarted - Script execution completed."
echo ""

# echo "Open logs folder? Press Y to open, any other key to not: " -NoNewLine

# $key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');

# if ($key.Character -eq 'y')
# {	
# 	explorer "\\192.168.1.25\config\netdaemon5\logs"
# }

cd "$solution_path/../scripts"
