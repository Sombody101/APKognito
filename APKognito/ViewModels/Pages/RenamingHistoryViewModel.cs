using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using System.Collections.ObjectModel;

namespace APKognito.ViewModels.Pages;

public partial class RenamingHistoryViewModel : ObservableObject, IViewable
{
    private readonly ConfigurationFactory configFactory;

    #region Properties

    [ObservableProperty]
    private ObservableCollection<RenameSession> _renameSessions = [
#if DEBUG
        new(["1;;com.google.idk;;com.apkognito.idk", "0;;com.facebook.spooky;;com.apkognito.spooky"], 1098234423),
        new(["1;;com.clouds.someapp;;com.apkognito.someapp"], 10955824023),
        new(["0;;com.fire.whatif;;com.apkognito.whatif", "0;;com.amazon.somedumbapp;;com.apkognito.newcoolappidk",
            "0;;com.notspyware.spyware;;com.apkognito.stillspyware", "1;;com.oof.oof;;com.apkognito.oof"], 1098624023),
#endif
        ];

    #endregion Properties

    public RenamingHistoryViewModel(ConfigurationFactory _configFactory)
    {
        configFactory = _configFactory;
    }

    #region Commands

    [RelayCommand]
    public async Task RefreshRenameSessions()
    {
        RenameSessionList storedSessions = configFactory.GetConfig<RenameSessionList>();
        var sessions = storedSessions.RenameSessions;

        RenameSessions.Clear();

        if (sessions.Count is 0)
        {
            //RenameSessions.Add(RenameSession.Empty);
            return;
        }

        // Add a delay so the user knows something happened
        await Task.Delay(200);

        foreach (RenameSession session in sessions)
        {
            RenameSessions.Add(session);
        }
    }

    #endregion Commands
}