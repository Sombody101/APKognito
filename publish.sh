#!/bin/bash

[[ ! "$(which hub)" ]] && {
    echo "hub is not installed. Cannot create release."
    exit 1
}

[[ ! "$1" ]] && {
    echo "No github token provided."
    exit 1
}

# Safe Change Directory
scd() {
    pushd "$*" || {
        echo "Failed to cd into '$*'"
        exit
    }
}

# Safe Change Back
scb() {
    popd || {
        echo "Failed to cd back into '$OLDPWD'"
        dirs -c
        exit
    }
}

readonly release_type="Beta"
readonly release_tag_prefix="v"
readonly publish_profile="./APKognito/Properties/PublishProfiles/FolderProfile.pubxml"
readonly git_remote_url="https://github.com/Sombody101/APKognito"

readonly build_path="./APKognito/bin/Release/net8.0-windows/publish/win-x64"
! dotnet publish -c Release -p:PublishProfile="$publish_profile" && exit "$?"

appversion="$($build_path/APKognito.exe --version | tr -d '\000-\037\177')"
readonly appversion

echo
echo "Build version: |$appversion|"

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

commit_messages="${commit_messages}${NL}###### This was created by an auto publish script @ $(date -u)"

readonly commit_messages
readonly release_title="[$release_type] Release $appversion"
readonly release_tag="${release_tag_prefix}${appversion}"

echo
echo

echo "$release_tag"
echo "$release_title"
echo "$commit_messages"

! GITHUB_TOKEN="$1" hub release create -a "$build_path/$zip_file" -m "$release_title" -m "$commit_messages" "$release_tag" && exit "$?"

echo "Release created with the tag $release_tag"