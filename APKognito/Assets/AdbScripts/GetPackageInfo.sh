#!/system/bin/sh

readonly NL=$'\n'
output=

for pkg in $(pm list packages -3 -f | cut -d ':' -f 2); do
    package_name="${pkg##*=}"
    assets_size=$(du -s "$/storage/emulated/0/Android/obb/$package_name" 2>/dev/null | cut -f 1)
    assets_size="${assets_size:--1}"

    package_path="${pkg%=*}"
    package_size=$(du -s "$package_path" | cut -f 1)

    package_data_size=$(du -s "/storage/emulated/0/Android/data/$package_name" 2>/dev/null | cut -f 1)
    package_data_size="${package_data_size:--1}"

    output="$output$NL$package_name|$package_size|$assets_size|$package_data_size"
done

echo "$output"