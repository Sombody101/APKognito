#!/bin/bash

[[ ! $(which cwebp) ]] && {
    echo "cwebp required."
    exit
}

echo "Optimizing images"

for file in $(find -name "*.png"); do
    echo "$file"
    cwebp "$file" -o "${file%.png}.webp" && rm "$file"
done

echo "Changing image references"

for file in $(find -name "*.md"); do
    echo "$file"
    sed -i -e 's/\.png/\.webp/g' $file
done
