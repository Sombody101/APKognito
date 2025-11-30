#!/bin/bash

[[ ! $(which cwebp) ]] && {
    echo "cwebp required."
    exit
}

echo "Optimizing images"

while IFS= read -r -d '' file; do
    echo "$file"
    cwebp "$file" -o "${file%.png}.webp" && rm "$file"
done < <(find . -name "*.png" -print0)

echo "Changing image references"

while IFS= read -r -d '' file; do
    echo "$file"
    sed -i -e 's/\.png/\.webp/g' "$file"
done < <(find . -name "*.md" -print0)
