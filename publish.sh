#!/bin/bash

###
# Exit codes:
#   1: Generic error
#   2: Invalid or missing input argument
#   3: Failed to change directory
#   4: Dependency is missing (jq, hub, etc)
###

[[ ! "$1" ]] && {
    echo "No github token provided."
    exit 2
}

[[ ! "$2" ]] && {
    echo "No VirusTotal API key provided."
    exit 2
}

[[ "$3" == "-p" ]] && {
    readonly prerelease=true
}

[[ ! "$(which jq)" ]] && {
    echo "jq is required to run."
    exit 4
}

[[ ! "$(which hub)" ]] && {
    echo "hub is required to run."
    exit 4
}

# Safe Change Directory
scd() {
    pushd "$*" || {
        echo "Failed to cd into '$*'"
        exit 3
    }
}

# Safe Change Back
scb() {
    popd || {
        echo "Failed to cd back into '$OLDPWD'"
        dirs -c
        exit 3
    }
}

if [[ "$prerelease" ]]; then
    readonly release_type="Prerelease"
    readonly release_tag_prefix="d"
else
    readonly release_type="Beta"
    readonly release_tag_prefix="v"
fi

readonly publish_profile="./APKognito/Properties/PublishProfiles/FolderProfile.pubxml"
readonly git_remote_url="https://github.com/Sombody101/APKognito"

readonly build_path="./APKognito/bin/Release/net8.0-windows/publish/win-x64"
! dotnet publish -c Release -p:PublishProfile="$publish_profile" && exit "$?"

appversion="$($build_path/APKognito.exe --version | tr -d '\000-\037\177')"
readonly appversion

echo
echo "Build version: |$appversion|"

echo "Uploading to VirusTotal"
permlink="https://www.virustotal.com/gui/file-analysis/$(curl -X POST https://www.virustotal.com/api/v3/files -H "x-apikey: $2" --form file=@"$build_path/APKognito.dll" | jq -r '.data.id')"
readonly permlink

readonly zip_file="APKognito-$appversion.zip"

# Zip build files (except for other zip files)
scd "$build_path"
files="$(find . -type f -not -name "*.zip")"

! zip -r "$zip_file" "." -i $files && {
    result="$?"
    dirs -c
    exit "$result"
}
scb

# Build an update list (all commits since last release tag)
commit_messages="$(git log "$(git describe --tags --abbrev=0)"..@ --oneline --no-merges)"

readarray -t split <<<"$commit_messages"

NL=$'\n'
commit_messages=
for message in "${split[@]}"; do
    read -ra splitMessage <<<"$message"
    commit_messages="${commit_messages} - [${splitMessage[0]}]($git_remote_url/tree/${splitMessage[0]}): ${splitMessage[*]:1}${NL}"
done

commit_messages="${commit_messages}${NL}[VirusTotal for v${appversion}](${permlink})${NL}${NL}###### This was created by an auto publish script @ $(date -u)"

readonly commit_messages
readonly release_title="[$release_type] Release $appversion"
readonly release_tag="${release_tag_prefix}${appversion}"

echo
echo

echo "$release_tag"
echo "$release_title"
echo "$commit_messages"

! GITHUB_TOKEN="$1" hub release create -a "$build_path/$zip_file" -m "$release_title" -m "$commit_messages" "$release_tag" "$3" && exit "$?"

echo "Release created with the tag $release_tag"
echo "Syncing remote tags..."
git pull

echo "Cleaning up"
rm "$build_path/$zip_file"

[[ "$*" =~ --gen ]] && {
    echo "Release link:"
    echo "[$release_tag](https://github.com/Sombody101/APKognito/releases/tag/$release_tag)"
}
