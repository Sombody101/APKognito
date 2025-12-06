## Summary

APKognito, as of v2.2.0, offers two methods for renaming packages.

### **Classic**

Brute force renames the package by replacing any instance of the company identifier of a given package name. This works for most packages and is the default option.

### **Bootstrap**

Rather than renaming the internals of a package, this method injects a new activity which bootstraps the original. This allows for significantly faster renames, and completely custom package names,
but comes with the caveat of not working for many packages.

Currently, the bootstrapper's Java source code isn't public. This isn't because I'm gatekeeping it, but simply because I haven't committed it yet. You can still find the Smali source [here](https://github.com/Sombody101/APKognito/blob/master/APKognito.ApkLib/Editors/BootstrapAssets/Bootstrap.smali), which is a relatively small file and should be somewhat readable.