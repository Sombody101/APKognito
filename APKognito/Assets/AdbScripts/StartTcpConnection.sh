#!/system/bin/sh

# Enable Wi-Fi 
svc wifi enable

# Get device IP address
local_ip=$(ip addr show wlan0 | grep "inet " | awk '{print $2}' | cut -d/ -f1)

# Print IP address for user reference
echo "Device IP address: $local_ip" 

# Restart ADB in TCP mode
adb tcpip 5555 

# Display success message
echo "ADB over Wi-Fi enabled. Connect using 'adb connect $local_ip:5555'"