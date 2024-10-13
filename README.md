<!-- Light/dark mode -->
<p align="center">
  <img align="center" width="50%" src="./APKognito/Assets/Logos/dark-wide.png#gh-dark-mode-only">
  <img align="center" width="50%" src="./APKognito/Assets/Logos/light-wide.png#gh-light-mode-only">
</p>

<p align="center">
  Rename Android APK files to prevent naming conflicts when debugging several apps on one device!
</p>

![APKognito example](./gitassets/APKognito%20Example.png)


> [!IMPORTANT]
Version numbering is going to change after `v1.5`. So, starting with `v1.6`, versions will look like this: `v1.6.9051.33421`.
Debug builds will be constant at `d1.0.0.0` (using a `d` prefix to prevent debug releases from updating on user machines using a publish).


# Build

`APKognito` uses [.Net Core 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). Ensure you have the SDK for it prior to building.

If you have Visual Studio installed, you can build it with that.

If you don't have Visual Studio but still have the .Net SDK, you can build it from
Powershell with this command:

```powershell
$ cd "C:\path\to\APKognito"
$ dotnet publish
```


# Contributing

![Alt](https://repobeats.axiom.co/api/embed/845c6a1e7b56de71e80b4a2c5969f7206d1eec8c.svg "Repobeats analytics image")


# License

`APKognito` is distributed under the GPL license, meaning any forks of it must have their source code made public on the internet. See the [LICENSE](./LICENSE.txt) for details.


# Notice

> [!CAUTION]
This application is meant for Android package debugging environments and workflows only. I am not responsible for the way `APKognito` is used or where it is used.

Expect all updates under the `[Beta]` tag to have bugs.