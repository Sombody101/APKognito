namespace APKognito.ApkMod.Automation;

/*
 * This is to store a custom procedure for renaming packages.
 * The format for now is:
 * 
 * @unpack ; Defined in the CommandStage enum
 * 
 * mkdir "/something"
 * cp "/other/data.json" "/something"
 */

public record AutoConfigModel
{
    public List<RenameStage> Stages { get; private set; } = [];

    public Version? ConfigVersion { get; set; }

    public void Organize()
    {
        Stages = [.. Stages.OrderBy(s => s.Stage)];
    }

    public RenameStage? GetStage(CommandStage stage)
    {
        return Stages.Find(s => s.Stage == stage);
    }
}

public record RenameStage
{
    public RenameStage(CommandStage stage)
    {
        Stage = stage;
    }

    public readonly CommandStage Stage;

    public readonly List<Command> Commands = [];
}

public record Command
{
    public Command(string name, IReadOnlyCollection<string> arguments)
    {
        Name = name;
        Arguments = arguments;
    }

    public readonly string Name;

    public readonly IReadOnlyCollection<string> Arguments;
}

public enum CommandStage
{
    /// <summary>
    /// Right after the package APK has been extracted.
    /// </summary>
    Unpack,

    /// <summary>
    /// Right before directories are renamed.
    /// </summary>
    Directory,

    /// <summary>
    /// Right before library files are renamed.
    /// </summary>
    Library,

    /// <summary>
    /// Right before smali bytecode files are renamed.
    /// </summary>
    Smali,

    /// <summary>
    /// Right before asset files are renamed (OBBs and bundles).
    /// </summary>
    Assets,

    /// <summary>
    /// Right before the package is packed.
    /// </summary>
    Pack
}