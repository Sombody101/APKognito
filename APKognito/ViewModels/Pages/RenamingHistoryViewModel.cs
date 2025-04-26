using APKognito.Configurations;
using APKognito.Configurations.ConfigModels;
using APKognito.Models;
using APKognito.Utilities.MVVM;
using System.Collections.ObjectModel;

namespace APKognito.ViewModels.Pages;

public partial class RenamingHistoryViewModel : ObservableObject, IViewable
{
    private readonly RenameSessionList storedSessions;

    #region Properties

    [ObservableProperty]
    public partial Visibility NoHistoryPanelVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial Visibility HistoryPanelVisibility { get; set; } = Visibility.Visible;

    [ObservableProperty]
    public partial ObservableCollection<RenameSession> RenameSessions { get; set; } = [
#if DEBUG
        new([(false, "com.google.idk", "com.apkognito.idk"),
            (false, "com.facebook.spooky", "com.apkognito.spooky")], 1098234423),
        new([(false, "com.clouds.someapp", "com.apkognito.someapp")], 10955824023),
        new([(false, "com.fire.whatif", "com.apkognito.whatif"),
            (false, "com.amazon.somedumbapp", "com.apkognito.newcoolappidk"),
            (false, "com.notspyware.spyware", "com.apkognito.stillspyware"),
            (true, "com.oof.oof", "com.apkognito.oof")], 1098624023),
#endif
        ];

    #endregion Properties

    public RenamingHistoryViewModel(ConfigurationFactory _configFactory)
    {
        storedSessions = _configFactory.GetConfig<RenameSessionList>();
    }

    #region Commands

    [RelayCommand]
    public async Task RefreshRenameSessionsAsync()
    {
        List<RenameSession> sessions = new(storedSessions.RenameSessions);
        sessions.Reverse();

        RenameSessions.Clear();

        // Add a delay so the user knows something happened
        await Task.Delay(10);

        if (sessions.Count is 0)
        {
            NoHistoryPanelVisibility = Visibility.Visible;
            HistoryPanelVisibility = Visibility.Collapsed;
        }
        else
        {
            NoHistoryPanelVisibility = Visibility.Collapsed;
            HistoryPanelVisibility = Visibility.Visible;
        }

        foreach (RenameSession session in sessions)
        {
            RenameSessions.Add(session);
        }
    }

    #endregion Commands
}