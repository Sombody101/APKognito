---
title: Anchor - Better Portability
---

By default, APKognito stores configurations, utilities, updates, and logs in `%APPDATA%\APKognito`. While standard, this setup limits portability by locking the application to a specific system directory.

To support users running APKognito in virtual machines or those who prefer a more self-contained setup, we introduced the Anchor file. The Anchor file allows you to override the default data location, giving you control over where APKognito stores its files.

## Anchor Setup

To use an Anchor file, you must create a file named `anchor.toml` and place it in the same directory as the APKognito binary. APKognito will ignore any other file name.

### Configuration

The file uses the [TOML](https://toml.io/en/) format. Currently (as of v2.2.1), it supports only one property: `DataRoot`.

-   Absolute Paths: Redirect data to a specific drive or folder (e.g., `D:\Tools\APK-Data`).
-   Relative Paths: Redirect data relative to the directory where APKognito is launched.
-   Current Directory (`.`): If you set the path to `.`, APKognito will store its data directly inside its current folder without creating a subdirectory.

!!! note
    Regardless of how you define the path, APKognito automatically converts it to an absolute path upon detection.

### Example Anchor File

If you want APKognito to store its data in a directory with a custom name, in this example, called `kognito-configs`, then this is what your configuration would look like:

```toml
DataRoot = "./kognito-configs"
```

Excluding path characters (`kognito-configs` instead of `./kognito-configs`) would also work, but this ensures nothing breaks in future updates.

If you don't want a subdirectory to contain all APKognito created folders and files, then specify the current directory.

```toml
DataRoot = "."
```

!!! warning
    This isn't recommended as uninstalling APKognito via the settings menu recursively deletes the configured data directory. If this example Anchor were in use, then it would delete _everything_ in the directory containing the APKognito binary, including other files you probably don't want deleted.

---

API formatted documentation will replace this page once more properties have been implemented.
