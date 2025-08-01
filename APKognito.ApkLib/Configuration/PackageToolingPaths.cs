﻿namespace APKognito.ApkLib.Configuration;

public sealed record PackageToolingPaths
{
    /// <summary>
    /// The path to the Java install.
    /// </summary>
    public string JavaExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// The file path to apktool.jar
    /// </summary>
    public string ApkToolJarPath { get; set; } = string.Empty;

    /// <summary>
    /// The file path to apktool.bat
    /// </summary>
    public string ApkToolBatPath { get; set; } = string.Empty;

    /// <summary>
    /// The file path to uber-apk-signer.jar
    /// </summary>
    public string ApkSignerJarPath { get; set; } = string.Empty;
}
