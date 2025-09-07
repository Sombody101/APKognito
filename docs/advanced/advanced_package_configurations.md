---
title: Advanced configuration
---

<!-- md:meta config | adv-rename.json -->

## Regex

### File regex pattern

<!-- md:flag required -->
<!-- md:meta text | package_replace_regex -->

This is a regex used throughout the package renaming process. It's default value is `(?<=[./_])({value})(?=[./_])`. The substring `{value}` is replaced with the original package company name one the renaming process starts. The button to the right of the text box is a reset button which will reset the regex string to its default value.

!!! warning

    It's important that the substring `{value}` be present and that the regex is valid, or the renaming process _will not work_. If you're trying to edit the regex, use [regex.101](https://regex101.com/) or other tools to make sure it won't fail.

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

## OBB renaming

### Extra OBB rename paths

<!-- md:meta text | rename_obbs_internal_extras -->

This box is disabled when the `Rename OBB Files Internally` toggle is disabled.

Every line in this textbox will be used as an internal file path in each OBB file found.
For example, the if the path `aa/Android/bin/tts/index.json` is found in an OBB file, that file will be extracted into memory, renamed, then repacked. No error or warning will be triggered if it's not found.

Each item can be either comma or newline separated. They will be newline separated regardless of the method used the next time APKognito loads the saved values.

## Java options

### Java flags

<!-- md:meta text | java_flags -->

Additional flags to be injected into the Java process.

The default value for this option is `--enable-native-access=ALL-UNNAMED` and allows apps to call methods which are implemented in native code.
This option was only required due to security changes made by Oracle and is requried in order to use Uber APK Signer.

If a rename fails and the output logs say:

<!-- Jimmyrigged to high hell, but it works. -->
<div class="highlight">
<pre>
<span></span><code style="color: red">Unrecognized option: --enable-native-access=ALL-UNNAMED
Error: Could not create the Java Virtual Machine
Error: A fatal exception has occurred. Program will exit.
</code></pre></div>

Simply clear the Java flags textbox, save your changes, and re-run the renaming process. This error is caused when using an older JDK or JRE
version that was made prior to the indroduction of this flag.

!!! info
Note, this is for the Java executable, not the .jar file that Java is running.

### Auto rename configs

<!-- md:meta toggle | auto_package_config_enabled -->

<!-- md:meta text | auto_package_config -->

!!! danger

    This feature can pose a huge security risk and was only implemented to address certain packages. 99% of packages ***do not need this!***

    Only use this if instructed by an active maintainer of APKognito to solve an issue, or if you trust the source of the script!

Auto rename configs use explicit UNIX-like commands to manipulate the renaming process in ways that would be too overly complex to hard-code into APKognito.
All commands are manually implemented wrappers for operations defined in C# ([command bindings](https://github.com/Sombody101/APKognito/tree/master/APKognito/ApkMod/Automation/CommandBindings)).

More information on auto rename configs can be found in the [auto config API](./auto_config_api.md) guide.

## Buffers

### Smali cutoff limit

<!-- md:meta number | smali_cutoff_limit -->

The file size limit before a Smali file is streamed rather than fully loaded into memory, in KB. Defaults to `1024` (KB).

### Smali buffer size.

<!-- md:meta number | smali_buffer_size -->

The buffer size for all streamed Smali files, in KB. Defaults to 64 (KB).

!!! note

    This only applies to files larger than the Smali cutoff limit.

### Scan file before rename

<!-- md:meta toggle | scan_smali_before_rename -->

Scans each Smali file for an instance of the original package name. If one is not found, the file is skipped.
