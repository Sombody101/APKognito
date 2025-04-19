#!/bin/bash

print_help() {
    echo "Usage: bump.sh [major | minor | (patch,build)]"
    echo "Bumps the version number of the raw string version by one (requires a file called 'version' to exist with a tri-segment version)."
    echo
    echo "Example:"
    echo "bash bump.sh major"
}

[[ "$1" == "--help" ]] && {
    print_help
    exit
}

[[ ! -f "./version" ]] && {
    echo "No version file found!"
    exit 1
}

readonly version="$(cat version)"
IFS='.' read -ra PARTS <<<"$version"

bump_segment() {
    local index="$1"
    [[ ! -v PARTS["$index"] ]] && {
        echo "Invalid version segment specified."
        exit 1
    }

    PARTS["$index"]=$((PARTS["$index"] + 1))
}

bump_index=2

while [[ $# -gt 0 ]]; do
    case "$1" in
    major)
        bump_index=0
        ;;

    minor)
        bump_index=1
        ;;

    patch | build)
        bump_index=2
        ;;

    *)
        print_help
        exit 1
        ;;
    esac

    shift
done

bump_segment "$bump_index"

readonly new_version="${PARTS[0]}.${PARTS[1]}.${PARTS[2]}"
echo "$new_version" >"version"
echo "Version bump $version > $new_version"
