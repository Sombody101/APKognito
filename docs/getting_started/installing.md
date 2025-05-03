---
title: Installing APKognito
---

The most recent version of APKognito can be found [here](https://github.com/Sombody101/APKognito/releases/latest).
Simply download the fist .ZIP file named `APKognito-{version}.zip`, where `{version}` is the respective publish version.

![APKognito Example Release](../images/apkognito-example-release.png)

!!! warning "Understanding Version Prefixes"

    APKognito versions are set up in a specific way, and you'll be able to tell what kind of
    build you're interacting with based on the prefix used for the release tag:

    * `v`: Any version that starts with `v` (e.g., `v2.0.0`) indicates a **Public Release**.
    * `pd`: Stands for **Public Debug**. These builds are halfway to being full Debug builds. They contain some tools or utilities that can help with diagnosing user issues, or even patches that will eventually reach Public Release builds, and are only ever given to address specific issues. This helps to prevent pushing out many updates so close to each other.
    * `d`: Indicates a **Debug** build of APKognito. The only way to obtain this is to build APKognito manually. This will also assign the constant version `d1.0.0` (or, `1.0.0.0` when viewing via the file properties dialog from File Explorer).

    ### ***Why is this important?***
    
    APKognito will only update (automatically or manually, via the Settings menu) to builds of the *same type*. So, if you're using a **Public Debug**, then APKognito
    will never update to a **Public Release**. If you're using a **Debug** build, then APKognito will never update (because Debug builds are never published).

    The version file can be found in [AssemblyInfo.Version.cs](https://github.com/Sombody101/APKognito/blob/master/APKognito/AssemblyInfo.Version.cs).
