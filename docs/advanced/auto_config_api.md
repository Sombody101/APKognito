---
title: Auto config API
---

<div class="grid cards" markdown>

-   :material-code-tags: **[Config Syntax]** – Understand the syntax before learning the available commands

</div>

[Config Syntax]: config_syntax.md

## File additions

### Exclude

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

---

### Include

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

---

## File commands

### Create directory

<!-- md:api write -->

```asm
mkdir <target>
```

Creates the respective target directory. If the directory already exists, no action is taken.

??? example

    ```asm
    mkdir "/smali/newdir/"
    ```

---

### Move entry

<!-- md:api read write -->

```asm
mv <source> <target>
```

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

---

### Copy entry

<!-- md:api read write -->

```asm
cp <source> <target>
```

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

---

### Remove entry

<!-- md:api write -->

```asm
rm <target> [targets ...]
```

Removes a file or directory. This should be used sparingly.

??? example

    Removing files and directories (recursive by nature).

    ```asm
    rm "/assets/bin/" "/smali/com/something/Logger/" "/assets/images/activity-logo.svg"
    ```
