#!/system/bin/ksh
# devices dont have ksh, but most of them use ksh hidden in sh.
# it at least shuts shellcheck up.

#readonly NL=$'\n'
output="["

#for pkg in $(pm list packages -3 -f | cut -d ':' -f 2); do
pm list packages -3 -f | cut -c 9- | while read -r pkg; do
    package_name="${pkg##*=}"
    package_path="${pkg%=*}"

    item_sizes="$(du -s "$package_path" "/sdcard/Android/obb/$package_name" "/sdcard/Android/data/$package_name" 2>&1 | awk '{sub(/du:/,"-1"); print $1}')"
    set -A sizes $item_sizes

    # shellcheck disable=SC2154
    package_size="${sizes[0]}"
    assets_size="${sizes[1]}"
    data_size="${sizes[2]}"

    # Newtonsoft allows it, even with a trailing comma, so why not.
    # Also a lot easier than tracking output format between this and a parser.
    jsonline="{\"package_name\":\"$package_name\",\"package_path\":\"$package_path\",\"package_size\":$package_size,\"assets_size\":$assets_size,\"data_size\":$data_size},"
    output="${output}${jsonline}"
done

# Every line *could* be printed as it's created, but the time to make a write call is too long.
# It's faster to build the string then yeet it. (I saw +~30ms writing each line)
echo "${output}]"
