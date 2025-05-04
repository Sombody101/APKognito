---
title: Advanced configuration
---

<!-- md:meta config | adv-rename.json -->

## Regex

### File replace regex

<!-- md:flag required -->
<!-- md:meta text | package_replace_regex -->

This is a regex used throughout the package renaming process. It's default value is `(?<=[./_])({value})(?=[./_])`. The substring `{value}` is replaced with the original package company name one the renaming process starts. The button to the right of the text box is a reset button which will reset the regex string to its default value.

!!! warning

    It's important that the substring `{value}` be present and that the regex is valid, or the renaming process _will not work_. If you're trying to edit the regex, use [regex.101](https://regex101.com/) or other tools to make sure it won't fail.

### Thread count

<!-- md:flag wip -->
<!-- md:meta num | threads -->

Specifies the maximum number of threads that can work towards the renaming process.

!!! note

    This only specifies the *maximum* number of threads that can be used, it doesn't guarantee that many threads are going to be used, especially if the package is super small.

    This value can also be overridden by writing directly to the configuration file, but increasing the number of threads will not be beneficial if your systems CPU doesn't support
    the number of requested threads.

    [The case of creating too many threads](https://www.baeldung.com/cs/servers-threads-number#bd-1-advantages-and-disadvantages-of-threads-versus-processes)

## Rename options

### Rename libraries

<!-- md:meta toggle | rename_libs -->

This toggle determines if library binaries will be renamed (in the literal sense). Libraries are loaded at runtime based on their string name. So, sometimes renaming the name of the file is mandatory to make sure the linking process remains intact.

### Rename libraries internally

<!-- md:meta toggle | rename_libs_internal -->

Besides renaming binary files, sometimes there are native implementations to methods defined in Smali (Java) code. There's no need to worry about the binaries becoming corrupt as their renaming process only affects the string table sections. All code and binary sections are left untouched.

!!! example

    The format of these native link strings are:

    ```asm
    Java_<tld>_<classes ...>_<method>
    ```

    An example using realistic naming would be:

    ```asm
    Java_com_sombody101_QuestionableInvoker_ActionContextLocator_LocateContext
    ```

### Rename OBB files internally

<!-- md:meta toggle | rename_obbs_internal -->

There isn't an option for renaming OBB files because that's a standard part of the renaming process, but renaming them _internally_ isn't.

With this, the file entries in every found OBB file are searched through in memory. If an entry either has the word "catalog" inside it or is a direct match to an entry in the `Extra OBB Rename Paths` text box, then it's fully extracted into memory, renamed, then repacked into the OBB.

## OBB renaming extras

### Extra OBB rename paths

<!-- md:meta text | rename_obbs_internal_extras -->

This box is disabled when the `Rename OBB Files Internally` toggle is disabled.

Every line in this textbox will be used as an internal file path in each OBB file found.
For example, the if the path `aa/Android/bin/tts/index.json` is found in an OBB file, that file will be extracted into memory, renamed, then repacked. No error or warning will be triggered if it's not found.

Each item can be either comma or newline separated. They will be newline separated regardless of the method used the next time APKognito loads the saved values.

### Auto rename configs

<!-- md:meta toggle | auto_package_config_enabled -->

<!-- md:meta text | auto_package_config -->

!!! danger

    This feature can pose a huge security risk and was only implemented to address certain packages. 99% of packages ***do not need this!***

    Only use this if instructed by an active maintainer of APKognito to solve an issue, or if you trust the source of the script!

Auto rename configs use explicit UNIX-like commands to manipulate the renaming process in ways that would be too overly complex to hard-code into APKognito.
All commands are manually implemented wrappers for operations defined in C# ([command bindings](https://github.com/Sombody101/APKognito/tree/master/APKognito/ApkMod/Automation/CommandBindings)).

More information on auto rename configs can be found in the [auto config API](./auto_config_api.md) guide.
