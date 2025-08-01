using System.Diagnostics;
using System.IO.Compression;
using System.Xml;
using APKognito.ApkLib.Configuration;
using APKognito.ApkLib.Exceptions;
using Microsoft.Extensions.Logging;

namespace APKognito.ApkLib.Editors;

public sealed class PackageCompressor
{
    // This class doesn't implement a logger or reporter only because there isn't enough going on
    // that can be reported. Verbose logs will be implemented eventually, but nothing beyond that.

    private readonly CompressorConfiguration _compressorConfiguration;
    private readonly ILogger _logger;
    private readonly PackageToolingPaths _toolingPaths;
    private readonly PackageNameData _nameData;

    public PackageCompressor(CompressorConfiguration compressorConfig, PackageToolingPaths toolPaths, PackageNameData nameData)
        : this(compressorConfig, toolPaths, nameData, null)
    {
    }

    public PackageCompressor(CompressorConfiguration compressorConfig, PackageToolingPaths toolPaths, PackageNameData nameData, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(compressorConfig);
        ArgumentNullException.ThrowIfNull(toolPaths);
        InvalidConfigurationException.ThrowIfNull(nameData);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolPaths.JavaExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolPaths.ApkToolJarPath);

        _toolingPaths = toolPaths;
        _nameData = nameData;
        _logger = MockLogger.MockIfNull(logger);
        _compressorConfiguration = compressorConfig;
    }

    /// <summary>
    /// Unpacks an APK into the given <paramref name="outputDirectory"/>. The directory will be created if it doesn't exist.
    /// </summary>
    /// <param name="packagePath">The source APK file path.</param>
    /// <param name="outputDirectory">The directory to unpack the APK into.</param>
    /// <param name="overwrite">Overwrites the output directory if it already exists.</param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="PackageUnpackFailedException"></exception>
    public async Task UnpackPackageAsync(string? packagePath = null, string? outputDirectory = null, bool overwrite = true, CancellationToken token = default)
    {
        packagePath = BaseRenameConfiguration.Coalesce(packagePath, _nameData.FullSourceApkPath);
        _logger.LogInformation("Unpacking {PackagePath}", Path.GetFileName(packagePath));

        await UnpackPackageInternalAsync(
            packagePath,
            BaseRenameConfiguration.Coalesce(outputDirectory, _nameData.ApkAssemblyDirectory),
            overwrite,
            token
        );
    }

    /// <summary>
    /// Packs the directory <paramref name="unpackedPackageDirectory"/> into the APK <paramref name="outputPackageFilePath"/>.
    /// </summary>
    /// <param name="unpackedPackageDirectory">The directory path to the unpacked APK.</param>
    /// <param name="outputPackageFilePath">The output file name </param>
    /// <param name="overwrite">Overwrites the output package if it already exists.</param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="DirectoryNotFoundException"></exception>
    /// <exception cref="PackagePackFailedException"></exception>
    public async Task<string> PackPackageAsync(string? unpackedPackageDirectory = null, string? outputPackageFilePath = null, bool overwrite = true, CancellationToken token = default)
    {
        _logger.LogInformation("Packing APK...");

        // Repacked and placed into the assembly directory because the package still needs to be signed. The caller can override this value, though.
        string outputPackagePath = BaseRenameConfiguration.Coalesce(
            outputPackageFilePath,
            () => Path.Combine(_nameData.ApkAssemblyDirectory, $"{_nameData.NewPackageName}.unsigned.apk")
        );

        await PackPackageInternalAsync(
            BaseRenameConfiguration.Coalesce(unpackedPackageDirectory, _nameData.ApkAssemblyDirectory),
            outputPackagePath,
            overwrite,
            token
        );

        return outputPackagePath;
    }

    /// <summary>
    /// Signs and aligns a package.
    /// </summary>
    /// <param name="unsignedPackageFilePath">The source unsigned APK path.</param>
    /// <param name="signedPackageFilePath">The output APK path. (Ignored when <paramref name="overwrite"/> is <see langword="true"/>)</param>
    /// <param name="overwrite">Overwrites the source APK.</param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="PackageSignFailedException"></exception>
    public async Task SignPackageAsync(string? unsignedPackageFilePath = null, string? signedPackageFilePath = null, bool fixOutputNames = true, CancellationToken token = default)
    {
        _logger.LogInformation("Signing APK...");

        await SignPackageInternalAsync(
            BaseRenameConfiguration.Coalesce(unsignedPackageFilePath, () => Path.Combine(_nameData.ApkAssemblyDirectory, $"{_nameData.NewPackageName}.unsigned.apk")),
            BaseRenameConfiguration.Coalesce(signedPackageFilePath, _nameData.RenamedOutputDirectoryInternal),
            fixOutputNames,
            token
        );
    }

    /// <summary>
    /// Assigns the package names to the shared <see cref="PackageNameData"/>. This is required in order for the rest of the renaming
    /// process to function.
    /// </summary>
    public void GatherPackageMetadata(string? manifestPath = null)
    {
        manifestPath = BaseRenameConfiguration.Coalesce(manifestPath, () => Path.Combine(_nameData.ApkAssemblyDirectory, "AndroidManifest.xml"));
        _nameData.OriginalPackageName = GetPackageName(manifestPath);

        (
            _,
            _nameData.OriginalCompanyName,
            _nameData.NewPackageName
        ) = SplitPackageName(_nameData);

        // Also set the output directory as long as a base directory is set and a explicit directory is not.
        if (_nameData.RenamedPackageOutputBaseDirectory is not null)
        {
            if (_nameData.RenamedPackageOutputDirectory is not null)
            {
                throw new InvalidConfigurationException("The RenamedPackageOutputDirectory must be null if RenamedPackageOutputBaseDirectory is set.");
            }

            _nameData.RenamedOutputDirectoryInternal = Path.Combine(_nameData.RenamedPackageOutputBaseDirectory, PackageUtils.GetFormattedTimeDirectory(_nameData.NewPackageName));
        }
        else
        {
            if (_nameData.RenamedPackageOutputDirectory is null)
            {
                throw new InvalidConfigurationException("Either RenamedPackageOutputBaseDirectory or RenamedPackageOutputDirectory must be set to valid paths.");
            }

            _nameData.RenamedOutputDirectoryInternal = _nameData.RenamedPackageOutputDirectory!;
        }

        _ = Directory.CreateDirectory(_nameData.RenamedOutputDirectoryInternal);
    }

    private async Task UnpackPackageInternalAsync(string packagePath, string outputDirectory, bool overwrite, CancellationToken token)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException($"APK file not found at: {packagePath}", packagePath);
        }

        _ = Directory.CreateDirectory(outputDirectory);

        IEnumerable<string> args = new string[]
        {
            "-jar", _toolingPaths.ApkToolJarPath, "d", overwrite ? "-f" : string.Empty, packagePath, "-o", outputDirectory
        }.Where(arg => arg is not null);

        CliToolExecutionResult result = await RunCliCommandAsync(_toolingPaths.JavaExecutablePath, args, token);

        if (result.IsError)
        {
            throw new PackageUnpackFailedException(result.ExitCode, $"ApkTool unpack failed. Exit Code: {result.ExitCode}", result.StdErr, result.Command);
        }
    }

    private async Task PackPackageInternalAsync(string unpackedPackageDirectory, string outputPackageFilePath, bool overwrite, CancellationToken token)
    {
        if (!Directory.Exists(unpackedPackageDirectory))
        {
            throw new DirectoryNotFoundException($"Unpacked package directory not found at: {unpackedPackageDirectory}");
        }

        string? outputDir = Path.GetDirectoryName(outputPackageFilePath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            _ = Directory.CreateDirectory(outputDir);
        }

        List<string> args =
        [
            "-jar", _toolingPaths.ApkToolJarPath, "b", overwrite ? "-f" : string.Empty, unpackedPackageDirectory
        ];

        if (!string.IsNullOrWhiteSpace(outputPackageFilePath))
        {
            args.Add("-o");
            args.Add(outputPackageFilePath);
        }

        CliToolExecutionResult result = await RunCliCommandAsync(_toolingPaths.JavaExecutablePath, args.Where(arg => arg is not null), token);

        if (result.IsError)
        {
            throw new PackagePackFailedException(result.ExitCode, $"ApkTool pack failed. Exit Code: {result.ExitCode}", result.StdErr, result.Command);
        }
    }

    private async Task SignPackageInternalAsync(string unsignedPackageFilePath, string signedPackageDirectory, bool fixOutputNames, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unsignedPackageFilePath);

        if (!File.Exists(unsignedPackageFilePath))
        {
            throw new FileNotFoundException($"Unsigned APK file not found at: {unsignedPackageFilePath}", unsignedPackageFilePath);
        }

        List<string> args =
        [
            "-jar", _toolingPaths.ApkSignerJarPath, "--allowResign", "-a", unsignedPackageFilePath
        ];

        if (!string.IsNullOrWhiteSpace(signedPackageDirectory))
        {
            args.Add("-o");
            args.Add(signedPackageDirectory);
        }

        CliToolExecutionResult result = await RunCliCommandAsync(_toolingPaths.JavaExecutablePath, args.Where(arg => arg is not null), token);

        if (result.IsError)
        {
            throw new PackageSignFailedException(result.ExitCode, $"ApkSign sign failed. Exit Code: {result.ExitCode}", result.StdErr, result.Command);
        }

        if (fixOutputNames)
        {
            // Rename the output APK
            // fullTrueName is also the OBB asset path when it doesn't have the file extension
            string fullTrueName = Path.Combine(_nameData.RenamedOutputDirectoryInternal, Path.GetFileName(unsignedPackageFilePath).Replace(".unsigned.apk", string.Empty));
            string newSignedName = $"{fullTrueName}.unsigned-aligned-debugSigned";

            File.Move($"{newSignedName}.apk", $"{fullTrueName}.apk", true);
            File.Move($"{newSignedName}.apk.idsig", $"{fullTrueName}.apk.idsig", true);
        }
    }

    public static string GetPackageName(string manifestPath)
    {
        using FileStream stream = File.OpenRead(manifestPath);
        return GetPackageName(stream);
    }

    public static string GetPackageName(Stream fileStream)
    {
        XmlDocument xmlDoc = new();
        xmlDoc.Load(fileStream);

        return xmlDoc.DocumentElement?.Attributes["package"]?.Value
            ?? throw new InvalidOperationException("Failed to get package name from AndroidManifest (XML).");
    }

    internal static (string packagePrefix, string replacementPackageName, string newCompanyName) SplitPackageName(PackageNameData nameData)
    {
        string[] split = nameData.OriginalPackageName.Split('.');

        /*
         * app => app
         * com.app => app
         * com.company.app => company
         * com.company.app.something... => company
         */
        string oldCompanyName = split.Length switch
        {
            1 => split[0],
            _ => split[1],
        };

        // Prefix, old company name, new package name
        return (split[0], oldCompanyName, nameData.OriginalPackageName.Replace(oldCompanyName, nameData.NewCompanyName));
    }

    public static long CalculateUnpackedApkSize(string apkPath, bool copyingFile = true)
    {
        try
        {
            long estimatedUnpackedSize = 0;

            using (ZipArchive archive = ZipFile.OpenRead(apkPath))
            {
                estimatedUnpackedSize = archive.Entries.Sum(entry => entry.Length);
            }

            if (!copyingFile)
            {
                // The source APK is deleted after being renamed
                estimatedUnpackedSize -= new FileInfo(apkPath).Length;
            }

            return estimatedUnpackedSize;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private async Task<CliToolExecutionResult> RunCliCommandAsync(string toolPath, IEnumerable<string> apkToolArgs, CancellationToken token = default)
    {
        apkToolArgs = [.. _compressorConfiguration.ExtraJavaOptions, .. apkToolArgs];
        using Process javaProc = JavaCreator.CreateManualProcess(toolPath, apkToolArgs);

        string command = $"{javaProc.StartInfo.FileName} {string.Join(" ", apkToolArgs)}";
        _logger.LogDebug("Running command: {Command}", command);

        try
        {
            if (!javaProc.Start())
            {
                string startError = $"Failed to start process: {javaProc.StartInfo.FileName}. Check Java path and permissions.";
                throw new InvalidOperationException(startError + $"\nCommand attempted: {command}");
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException($"Failed to start process. Is Java installed and the path correct? Path attempted: {javaProc.StartInfo.FileName}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"An unexpected error occurred while starting the Java process.", ex);
        }

        Task<string> standardErrorTask = javaProc.StandardError.ReadToEndAsync(token);

        await javaProc.WaitForExitAsync(token);

        return new CliToolExecutionResult(
            javaProc.ExitCode,
            standardErrorTask.Result,
            command
        );
    }

    private sealed class CliToolExecutionResult
    {
        public int ExitCode { get; }

        public string StdErr { get; }

        public string Command { get; }

        public bool IsError => ExitCode is not 0 || !string.IsNullOrWhiteSpace(StdErr);

        public CliToolExecutionResult(int exitCode, string standardError, string command)
        {
            ExitCode = exitCode;
            StdErr = standardError;
            Command = command;
        }
    }

    public class PackageUnpackFailedException : Exception
    {
        public int ToolExitCode { get; }

        public string StandardError { get; }

        public string Command { get; }

        public PackageUnpackFailedException(int exitCode, string message, string standardError, string command)
            : base(message + $"\nCommand: {command}\nStandard Error:\n{standardError}")
        {
            ToolExitCode = exitCode;
            StandardError = standardError;
            Command = command;
        }
    }

    public class PackagePackFailedException : Exception
    {
        public int ToolExitCode { get; }

        public string StandardError { get; }

        public string Command { get; }

        public PackagePackFailedException(int exitCode, string message, string standardError, string command)
             : base(message + $"\nCommand: {command}\nStandard Error:\n{standardError}")
        {
            ToolExitCode = exitCode;
            StandardError = standardError;
            Command = command;
        }
    }

    public class PackageSignFailedException : Exception
    {
        public int ToolExitCode { get; }

        public string StandardError { get; }

        public string Command { get; }

        public PackageSignFailedException(int exitCode, string message, string standardError, string command)
             : base(message + $"\nCommand: {command}\nStandard Error:\n{standardError}")
        {
            ToolExitCode = exitCode;
            StandardError = standardError;
            Command = command;
        }
    }
}
