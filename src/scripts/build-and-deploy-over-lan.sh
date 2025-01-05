# Script to build a .NET project, clean a directory, and move build output

# WARNING, MUCH IN THIS FILE WILL NEED TO BE CONFIGURED FOR YOUR PATHS, SETTINGS, ETC...

# READ COMMENTS THROUGHOUT SCRIPT CAREFULLY

# Local paths on your computer:
solution_path="/media/secondary/repos/netdaemon-home-assistant-apps/src/AllenStreetNetDaemonApps"
solution_filename="AllenStreetNetDaemonApps.csproj"

# ND folder after SSH commands below SSH into the server
build_path="/root/config/netdaemon4"

# Step 1: Run dotnet build
cd "$solution_path"

# Build the local project
dotnet build "$solution_path/$solution_filename"



# Step 2: Delete everything in the network share except 'logs' folder

# CHANGE THE PATH FOR THE CD IN HERE IF IT IS NOT YOUR ND PATH ON THE HA SERVER AFTER SSHING IN
ssh pockybum522-ha-ssh@192.168.1.25 -i /media/secondary/keys/DAVID-DESKTOP-2024-10-03 << 'EOF'
    cd "/root/config/netdaemon4"
    find . -not -name 'logs' -delete

    # Don't know why, but ND *really* wants this folder to exist even though we're only working with binaries
    mkdir -p "apps"
EOF


# Step 3: Copy local project over to where it should be on HA

echo "Built and deployed, now restarting ND addon"

# You will unfortunately need to turn off protected mode on the "terminal & ssh” addon for this line to work:
# Replace options in below line with your correct user, host, private key path, port, and addon ID
ssh pockybum522-ha-ssh@192.168.1.25 -i D:\keys\DAVID-DESKTOP-2024-10-03 -m hmac-sha2-512-etm@openssh.com 'sudo docker restart addon_c6a2317c_netdaemon4'

echo "ND addon restarted - Script execution completed."
echo ""
echo "Open logs folder? Press Y to open, any other key to not: " -NoNewLine

$key = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown');

if ($key.Character -eq 'y')
{	
	explorer "\\192.168.1.25\config\netdaemon4\logs"
}

cd "$solution_path/../scripts"
