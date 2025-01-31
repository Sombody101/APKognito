#!/system/bin/sh

# Output format
#
# <package name>|<package path>|<package size in bytes>|<assets size in bytes>|<save data size in bytes>

readonly NL=$'\n'
output=

log() {
    echo "$*"
}

[ "$1" != "--verbose" ] && {
    log() { :; }
}

for pkg in $(pm list packages -3 -f | cut -d ':' -f 2); do
    package_name="${pkg##*=}"
    log "working on: $package_name"
    assets_size=$(du -s "/sdcard/Android/obb/$package_name" 2>/dev/null | cut -f 1)
    log "asset size: $assets_size"
    assets_size="${assets_size:--1}"
    log "final asset size: $assets_size"

    package_path="${pkg%=*}"
    log "package path: $package_path"
    package_size=$(du -s "$package_path" | cut -f 1)
    log "package size: $package_size"

    package_data_size=$(du -s "/sdcard/Android/data/$package_name" 2>/dev/null | cut -f 1)
    log "data size: $package_data_size"
    package_data_size="${package_data_size:--1}"
    log "final data size: $package_data_size"

    output="$output$NL$package_name|$package_path|$package_size|$assets_size|$package_data_size"
done

echo "$output"