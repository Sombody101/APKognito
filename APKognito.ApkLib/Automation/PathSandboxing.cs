using System.Text;

namespace APKognito.ApkLib.Automation;

public class PathSandboxing
{
    /// <summary>
    /// Combines a user supplied package path with the real-world drive path. An <see cref="InvalidExtraPathException"/>
    /// will be thrown if the user supplied path escapes the package directory (i.e., "C:\Path\To\Package + /../../../../random-file.json")
    /// </summary>
    /// <param name="drivePath">The full package directory path.</param>
    /// <param name="userPath">The argument path.</param>
    /// <param name="noRoot">Specifies that the given argument is now allowed to target the virtual root directory.</param>
    /// <returns></returns>
    /// <exception cref="InvalidExtraPathException"></exception>
    public static string SafeCombine(string drivePath, string userPath, bool noRoot = true)
    {
        if (userPath.Any(char.IsControl))
        {
            throw new UnsafeExtraPathException($"The given path '{SanitizeStringBytes(userPath)}' contains control characters.");
        }

        drivePath = drivePath.TrimEnd('\\');
        string packagePath = userPath.Replace('/', '\\').TrimStart('\\');

        string normalizedBasePath = Path.GetFullPath(drivePath);
        string combinedPath = Path.GetFullPath(Path.Combine(normalizedBasePath, packagePath));

        if (noRoot && combinedPath.Equals(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnsafeExtraPathException($"The given path '{userPath}' attempts to modify the project root directory.");
        }

        return !combinedPath.StartsWith($"{normalizedBasePath}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? throw new UnsafeExtraPathException($"The given path '{userPath}' escapes the package directory.")
            : combinedPath;
    }

    private static string SanitizeStringBytes(string offending)
    {
        StringBuilder output = new();

        foreach (char c in offending)
        {
            if (!char.IsControl(c))
            {
                _ = output.Append(c);
                continue;
            }

            _ = output.Append($"(0x{(byte)c:x2})");
        }

        return output.ToString();
    }

    public class InvalidExtraPathException(string message) : Exception(message)
    {
    }

    public class UnsafeExtraPathException(string message) : Exception(message)
    {
    }
}
