---
title: Auto config API
---

<div class="grid cards" markdown>

-   :material-code-tags: **[Config Syntax]** – Understand the syntax before learning the available commands

</div>

[Config Syntax]: config_syntax.md

## File additions

### Exclude

Usage:

```asm
exclude <target> [targets ...]
```

Excludes the given file from the respective stage.

??? note

    The behavior of this command is dependant on the stage it's used in.
    When used within the `assets` stage, it will exclude _entries_ inside valid asset archive files, not the archive files themselves.

??? example

    ```asm
    exclude "/smali/com/somecompany/LoggerUtil/" ; This will exclude the directory from being renamed.
    ```

### Include

Usage:

```asm
include <target> [targets ...]
```

Includes the given file in the respective stage.

??? note

    The behavior of this command is dependant on the stage it's used in.
    When used within the `assets` stage, it will include _entries_ inside valid asset archive files, not the archive files themselves.

??? example

    ```asm
    exclude "/smali/com/somecompany/LoggerUtil/" ; This will include the directory to be renamed.
    ```

## File commands

### Create directory

Usage:

```asm
mkdir <target>
```

<!-- md:api write -->

Creates the respective target directory. If the directory already exists, no action is taken.

??? example

    ```asm
    mkdir "/smali/newdir/"
    ```

### Move entry

Usage:

```asm
mv <source> <target>
```

<!-- md:api read write -->

Moves a file or directory to a new location, renaming it if specified.

??? example

    Moving a directory (recursive by nature).

    ```asm
    mv '/assets/cool-image.svg' '/smali/com/coolsvile/lorax/cool-image.svg'
    ```

    Moving a file.

    ```asm
    mv '/assets/bin/Data/Managed/Metadata/global-metadata.dat' '/assets/global-metadata.dat'
    ```

### Copy entry

Usage:

```asm
cp <source> <target>
```

<!-- md:api read write -->

Copies a file or directory to a new location, renaming it if specified.

??? example

    Copying a directory (recursive by nature).

    ```asm
    cp "/assets/bin/Objects/" "/assets/bin/Managed/"
    ```

    Copying a file.

    ```asm
    cp "/assets/bin/Data/Managed/Metadata/global-metadata.dat" "/assets/global-metadata.dat"
    ```

### Remove entry

Usage:

```asm
rm <target> [targets ...]
```

<!-- md:api write -->

Removes a file or directory. This should be used sparingly.

??? example

    Removing files and directories (recursive by nature).

    ```asm
    rm "/assets/bin/" "/smali/com/something/Logger/" "/assets/images/activity-logo.svg"
    ```
