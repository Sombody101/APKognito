#!/bin/bash

[[ "$1" == "--help" ]] && {
    echo "Usage"
    echo "publish.sh <github token> <virus total token> [deb, pre (debug/pre-release)]"
    exit
}

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

libs=(
    jq
    hub
    zip
)

exit_code=
for lib in "${libs[@]}"; do
    [[ ! "$(which $lib)" ]] && {
        echo "$lib is required to run."
        exit_code=4
    }
done

[[ "$exit_code" ]] && exit $exit_code

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

publish_profile="ReleaseProfile.pubxml"

case "$3" in

"pre")
    echo "Building pre-release."
    publish_profile="PreReleaseProfile.pubxml"
    readonly release_type="Prerelease"
    readonly release_tag_prefix="pd"
    readonly extra_args="--prerelease"
    ;;

"deb")
    echo "Building public debug release."
    publish_profile="PublicDebugProfile.pubxml"
    readonly release_type="PublicDebug"
    readonly release_tag_prefix="d"
    readonly extra_args="--prerelease"
    ;;

*)
    readonly release_type="Release"
    readonly release_tag_prefix="v"
    ;;
esac

! bash "./generate_version.sh" "$release_type" && exit

readonly build_path="./APKognito/bin/Release/net8.0-windows/win-x64/"

echo "Using build profile: $publish_profile"
! dotnet build "./APKognito/APKognito.csproj" -c Release -p:PublishProfile="./APKognito/Properties/PublishProfiles/$publish_profile" && exit

appversion="$($build_path/APKognito.exe --version | tr -d '\000-\037\177')"
appversion="${appversion%.*}"
readonly appversion
readonly release_tag="${release_tag_prefix}${appversion}"

echo
echo "Build version |$appversion| (${release_tag})"

[[ "$2" != "-" ]] && {
    echo "Uploading to VirusTotal"
    permlink="https://www.virustotal.com/gui/file-analysis/$(curl -X POST https://www.virustotal.com/api/v3/files -H "x-apikey: $2" --form file=@"$build_path/APKognito.dll" | jq -r '.data.id')"
    readonly permlink
}

readonly zip_file="APKognito-${release_tag}.zip"

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

readonly git_remote_url="https://github.com/Sombody101/APKognito"

NL=$'\n'
commit_messages=
for message in "${split[@]}"; do
    read -ra splitMessage <<<"$message"
    commit_messages="${commit_messages} - [${splitMessage[0]}]($git_remote_url/tree/${splitMessage[0]}): ${splitMessage[*]:1}${NL}"
done

commit_messages="${commit_messages}${NL}[VirusTotal for ${release_tag}](${permlink})${NL}${NL}###### This was created by an auto publish script @ $(date -u)"

readonly commit_messages

release_title="Release $release_tag"
[[ "$release_type" != "Release" ]] && {
    release_title="[$release_type] $release_title"
}

readonly release_title

echo
echo

echo "$release_tag"
echo "$release_title"
echo "$commit_messages"

[[ "$1" != "-" ]] && {
    ! GITHUB_TOKEN="$1" hub release create -a "$build_path/$zip_file" -m "$release_title" -m "$commit_messages" "$release_tag" "$extra_args" && exit "$?"

    echo "Release created with the tag $release_tag"
    echo "Syncing remote tags..."
    git pull
}

echo "Cleaning up"
rm "$build_path/$zip_file"

echo "Release link:"
echo "[$release_tag](https://github.com/Sombody101/APKognito/releases/tag/$release_tag)"
