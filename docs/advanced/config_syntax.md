---
title: Auto config syntax
---

The auto config syntax was designed to be as simple as possible while maintaining a level of security for APKognito users.
In this article, we go over the syntax and how the scripts you write are handled by APKognito.

## Understanding auto config syntax

The auto config syntax uses explicit UNIX-like commands. This means that most commands use the same names as built-in Linux Bash commands and binaries.

Some examples include `mv`, `cp`, `mkdir`, etc.

### Commands

All commands are implemented as method bindings/wrappers for C# standard library methods
([command bindings]). To learn what commands are available, as well as their arguments and descriptions, visit the [auto config API](./auto_config_api.md) page.

[command bindings]: https://github.com/Sombody101/APKognito/tree/master/APKognito/ApkMod/Automation/CommandBindings

### Arguments

Just like commands for any app that has ever implemented commands, you can (and usually are required to) give arguments to change what a command does and what it targets.
For auto configs, however, arguments are limited to just string paths. This is due to a combination of factors, including laziness, but also because there aren't enough using this feature, let alone
using APKognito.

Argument paths must be in double or single quotes, and must originate from the root of the package directory to comply with [package sandboxing](#package-sandboxing).

???+ example

    ```asm
    cp '/assets/shrek.sh' "/" ; Copies... stuff to the package root directory
    cp 'assets/shrek.sh' "/"  ; Throws an error because
    ```

However, unlike regular OS utilities, auto config commands do not take positional flag arguments. This is likely to change in the future, but as of right now, the only
supported argument type is an absolute path.

#### File or directory?

When using a command such as `mv` or `cp`, arguments may end with a slash to suggest the item is a directory. If no slash is present, then APKognito will check
if a file exists with the given name, then check if a directory exists, and finally give an error stating no entry could be found.

If an argument has a slash, APKognito will skip right to testing for a directory and will give an error if no directory is found.

??? example

    ```asm
    mv
    ```

### Comments

Unlike most traditional programming languages, comments are denoted with a semicolon (`;`).

C#

```cs
// Move the file
System.IO.File.Move("/smali/something.smali", "/.");
```

vs.

Auto config

```asm
; move the file
mv '/smali/something.smali' '/.'
```

## What are stages?

A document is set up in stages which are denoted with a `@` prefix.

The supported stages are:

1. **Unpack**: Runs right after the package is unpacked.
1. **Directory**: Runs before directories are renamed.
1. **Library**: Runs before libraries are renamed (even if the user has this option disabled).
1. **Smali**: Runs before Smali files are renamed.
1. **Pack**: Runs before the package is repacked.
1. **Assets**: Runs before assets are copied/moved and renamed.
    - _Note_: This is only a _stage_. It does not allow you to modify or move the asset files. The `include` and `exclude` commands will only work on entries found within valid asset archives.

The stages will occur in the order they're listed in.

??? abstract
    The Assets stage used to run before the pack stage, meaning assets would be copied or moved and processed _before_ finalizing the package.
    This wasn't a huge issue, but didn't make sense as assets are almost always the largest part of any game or app and would take the longest to process.

Stages can be defined more than once and will be reorganized while parsing.

???+ example

    ```asm
    @pack
    mkdir "/funny-memes/"

    @smali
    exclude "/smali/com/some_file.smali"

    @unpack
    mv "/legit/file"

    @smali
    mv "/smali/com/me/app.smali" "/."
    ```

    This will be reorganized behind the scenes and effectively become:

    ```asm
    @unpack
    mv "/legit/file"

    @smali
    exclude "/smali/com/some_file.smali"
    mv "/smali/com/me/app.smali" "/."

    @pack
    mkdir "/funny-memes/"
    ```

## Package sandboxing

Auto configs have been set up in a way to help with security. As such, there are some limitations on what auto configs can do.

### Path arguments

Paths that are passed as command arguments are not allowed to escape the package directory and must be formatted as absolute paths, assuming the package root is the same as a drive root.

!!! example

    The following line will throw an `UnsafeExtraPathException` when used while renaming a package.

    ```asm
    ; The second argument attempts to escape the directory containing the package, so APKognito will halt execution and alert the user.
    mv "/valid/path/entry" "../non-valid/path/entry"
    ```

    This also fails even if you hide a parent directory navigator inside the path.

    ```asm
    mv "/valid/path/entry" "/valid/../../entry"
    ```

### Control characters in paths

Paths cannot contain any control binary characters (i.e., `\r`, `\t`, `\b`, `\0`, etc).
If any are found in any path arguments, an `UnsafeExtraPathException` will be thrown and the user will be alerted (and prompted with a cleaned string with all offending characters
reformatted in hex).

The exception to this is newline characters, which if present at any point in a line besides the end will slice the line in two, leading to a parsing error.
