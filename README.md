# APKognito

Rename Android APK files to prevent naming conflicts when debugging several apps on one device!


# Build

`APKognito` was built with [.Net Core 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). Ensure you have the SDK for it prior to trying to build.

If you have Visual Studio installed, you can build it with that.

If you don't have Visual Studio but still have the .Net SDK, you can build it from
Powershell with this command:

```ps
$ cd $path_to_apkognito_source
$ dotnet publish
```

# License

`APKognito` is distributed under the GPL license, meaning any forks of it must have their source code made public on the internet. See the [LICENSE](./LICENSE.txt) for details.


# Notice
> [!CAUTION]
This application is meant for Android package debugging environments and workflows only. I am not responsible for the way `APKognito` is used or where it is used.