---
title: Configurations
---

<!-- md:meta config | settings.json -->

APKognito offers both basic and advanced configurations. All basic configurations can be found under the card expander on the main package renaming page.

![basic configurations location](images/configurations/basic-configurations-location.webp)

There a several basic options that come build into a dropdown card menu on the main package renamer page.

### Java Executable

This is the path directing to the Java runtime executable. This is set every time APKognito opens and when the `Start Renaming` button is pressed.
If no path is found, then this field is left blank and the user will be warned about an invalid Java path when a rename job is attempted.

### Output Directory

<!-- md:meta path | apk_output -->

This is the directory that all renamed packages are placed into.

The default path is `%APPDATA%\APKognito\output`.

### Output APK Name

<!-- md:meta text | apk_replacement_name -->

This is the replacement company name of a package.

!!! example

    apk_replacement_name = `apkognito`

    `com.sombody101.myapp` -> `com.apkognito.myapp`

The default value for this is `apkognito`.

### Copy App Files

<!-- md:meta toggle | copy_when_renaming -->

This option is only useful if there isn't enough space on which ever drive `%TEMP%` is defined on (usually the C drive `%APPDATA%\Temp\`). Disabling this toggle will _move/delete_ the source app files immediately after their use, but before the process is finished. This is to preserve drive space.

!!! warning

    APKognito will not reverse the process if the renaming process fails while this is set to move files. You'll have to rebuild your project and get the original app again. A warning dialog will appear when this option is toggled.

### Push After Rename

<!-- md:meta toggle | push_after_rename -->

Each renamed package will be pushed to an ADB enabled device after being renamed. This toggle requires that platform tools is installed and a valid path is set in APKognito. You can also enter the command `:install-adb` in the Console Page to install and configure platform tools automatically.

### Open Advanced Options

<!-- md:meta page -->

This action card will bring you to the Advanced Renaming Options page. To learn more, go to the [Advanced configuration](advanced/advanced_package_configurations.md) guide.
