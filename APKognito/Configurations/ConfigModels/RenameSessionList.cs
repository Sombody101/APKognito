using APKognito.Models;
using MemoryPack;

namespace APKognito.Configurations.ConfigModels;

[MemoryPackable]
[ConfigFile("history.bin", ConfigType.MemoryPacked)]
public partial class RenameSessionList : IKognitoConfig
{
    [MemoryPackIgnore]
    public ConfigType ConfigType => ConfigType.MemoryPacked;

    public List<RenameSession> RenameSessions { get; set; } = [];

    public RenameSessionList()
    { }

    [MemoryPackConstructor]
    private RenameSessionList(List<RenameSession> renameSessions)
    {
        RenameSessions = renameSessions;
    }
}