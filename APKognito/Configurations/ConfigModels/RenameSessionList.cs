using APKognito.Models;

namespace APKognito.Configurations.ConfigModels;

[ConfigFile("history.bin", ConfigType.Bson)]
public partial class RenameSessionList : IKognitoConfig
{
    public List<RenameSession> RenameSessions { get; set; } = [];

    public RenameSessionList()
    { }

    private RenameSessionList(List<RenameSession> renameSessions)
    {
        RenameSessions = renameSessions;
    }
}
