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

ignore() {
    [[ $1 =~ ^(\.|-)$ ]]
}

publish_profile="ReleaseProfile"

case "$3" in

"pre")
    echo "Building pre-release."
    publish_profile="PreReleaseProfile"
    readonly release_type="Prerelease"
    readonly release_tag_prefix="pd"
    readonly extra_args="--prerelease"
    ;;

"deb")
    echo "Building public debug release."
    publish_profile="PublicDebugProfile"
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

readonly build_path="./APKognito/bin/Release/net9.0-windows/publish/win-x64/"

echo "Using build profile: $publish_profile"
! dotnet publish "./APKognito/APKognito.csproj" --property:WarningLevel=0 -c Release -p:PublishProfile="$publish_profile" && exit

appversion="$($build_path/APKognito.exe --version | tr -d '\000-\037\177')"
appversion="${appversion%.*}"
readonly appversion
readonly release_tag="${release_tag_prefix}${appversion}"

echo
echo "Build version |$appversion| (${release_tag})"

! ignore "$2" && {
    echo "Uploading to VirusTotal"

    if [[ -f $build_path/APKognito.dll ]]; then
        vt_upload_file="$build_path/APKognito.dll"
    else
        vt_upload_file="$build_path/APKognito.exe"
    fi

    return_id="$(curl -X POST https://www.virustotal.com/api/v3/files -H "x-apikey: $2" --form file=@"$vt_upload_file" | jq -r '.data.id')"
    
    [[ "$return_id" == null ]] && {
        echo "- Failed to upload app to VirusTotal."
    } || {
        echo "Using VTID: $return_id"

        permlink="https://www.virustotal.com/gui/file-analysis/$return_id"
        readonly permlink
    }

    unset return_id
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

echo "Release tag: $release_tag"
echo "Release title: $release_title"
echo "Commit messages:"
echo "$commit_messages"

! ignore "$1" && {
    ! GITHUB_TOKEN="$1" hub release create -a "$build_path/$zip_file" -m "$release_title" -m "$commit_messages" "$release_tag" "$extra_args" && exit "$?"

    echo "Release created with the tag $release_tag"
    echo "Syncing remote tags..."
    git pull
}

echo "Cleaning up"
rm "$build_path/$zip_file"

echo "Release link:"
echo "[$release_tag](https://github.com/Sombody101/APKognito/releases/tag/$release_tag)"
