using APKognito.Models;
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APKognito.Configurations.ConfigModels;

[MemoryPackable]
public partial class RenameSessionList : IKognitoConfiguration
{
    [MemoryPackIgnore]
    public string FileName => "history.bin";

    [MemoryPackIgnore]
    public ConfigType ConfigType => ConfigType.MemoryPacked;

    public List<RenameSession> RenameSessions = [];

    public RenameSessionList() { }

    [MemoryPackConstructor]
    private RenameSessionList(List<RenameSession> renameSessions)
    {
        RenameSessions = renameSessions;
    }
}
