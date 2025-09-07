using System.Collections.ObjectModel;

namespace APKognito.ApkLib.Automation;

public record AutoConfig
{
    public required ReadOnlyCollection<RenameStage> Stages { get; init; }

    public required Version ConfigVersion { get; init; }

    public required ReadOnlyDictionary<string, string> MetadataTable { get; init; }

    public RenameStage? GetStage(CommandStage stage)
    {
        return Stages.FirstOrDefault(s => s.Stage == stage);
    }
}

public class AutoConfigBuilder
{
    public List<RenameStage> Stages { get; private set; } = [];

    public Dictionary<string, string> MetadataTable { get; } = [];

    public AutoConfig Build()
    {
        List<RenameStage> stages = [.. Stages.OrderBy(s => s.Stage)];
        Version version = GetMetaVersion();

        return new()
        {
            Stages = stages.AsReadOnly(),
            ConfigVersion = version,
            MetadataTable = MetadataTable.AsReadOnly()
        };
    }

    public RenameStage? GetStage(CommandStage stage)
    {
        return Stages.Find(s => s.Stage == stage);
    }

    private Version GetMetaVersion()
    {
        string? foundVersion = MetadataTable.FirstOrDefault(pair => pair.Key is "version").Value;

        return foundVersion is null || !Version.TryParse(foundVersion, out Version? version)
            ? new(0, 0, 0, 0)
            : version;
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

    public Command(string name, IEnumerable<string> arguments)
    {
        Name = name;
        Arguments = arguments.ToList();
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
