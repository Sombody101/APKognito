using APKognito.Models;
using MemoryPack;

namespace APKognito.Configurations.ConfigModels;

[MemoryPackable]
[ConfigFile("history.bin", ConfigType.MemoryPacked)]
public partial class RenameSessionList : IKognitoConfig
{
    public List<RenameSession> RenameSessions { get; set; } = [];

    public RenameSessionList()
    { }

    [MemoryPackConstructor]
    private RenameSessionList(List<RenameSession> renameSessions)
    {
        RenameSessions = renameSessions;
    }
}