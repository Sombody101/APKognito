using APKognito.ApkLib.Configuration;
using System.IO;

namespace APKognito.ApkLib.Automation;

[AttributeUsage(AttributeTargets.Method)]
internal class CommandAttribute : Attribute
{
    public const int ANY = -1;
    public const int NONE = 0;

    /// <summary>
    /// The command name associated with the backing method.
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// The access type of the given command. Write/ReadWrite requires that all input paths are not the package directory.
    /// If any are, then an <see cref="AdvancedApkRenameSettings.UnsafeExtraPathException"/> will be thrown when verifying.
    /// </summary>
    public readonly FileAccess[] Accessors;

    /// <summary>
    /// The number of expected arguments. (-1 means any allowed)
    /// </summary>
    public readonly int ArgumentCount;

    public CommandAttribute(string name, int argCount = NONE, params FileAccess[] accessors)
    {
        if (argCount is not ANY && accessors.Length != argCount)
        {
            throw new ArgumentException($"{name} expects {argCount} arguments, but only {accessors.Length} access descriptors were given.");
        }

        Name = name;
        Accessors = accessors;
        ArgumentCount = argCount;
    }
}
