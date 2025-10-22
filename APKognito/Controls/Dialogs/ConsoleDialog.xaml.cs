using APKognito.AdbTools;
using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace APKognito.Controls.Dialogs;

public sealed partial class ConsoleDialog
{
    public ConsoleDialogViewModel ViewModel { get; }

    public ConsoleDialog(ContentPresenter? contentPresenter, string title = "Console", string initialCloseText = "Cancel")
        : base(contentPresenter)
    {
        Title = title;

        DataContext = this;
        ViewModel = new(initialCloseText);

        InitializeComponent();
    }

    public async Task RunInternalCommandAsync(string command)
    {
        await ViewModel.RunInternalAsync(command);
    }

    public async Task<AdbCommandOutput> RunAdbCommandAsync(string command)
    {
        return await ViewModel.RunAdbAsync(command);
    }

    public void Finished(string finishedText = "Close")
    {
        ViewModel.Finished(finishedText);
    }

    public static async Task<ContentDialogResult> RunInternalCommandAsync(
        string command,
        string title,
        string initialCloseText,
        string closeButtonText,
        ContentPresenter? presenter)
    {
        ConsoleDialog dialog = new(presenter, title, initialCloseText);
        Task<ContentDialogResult> showTask = dialog.ShowAsync();
        await dialog.RunInternalCommandAsync(command);
        dialog.Finished(closeButtonText);

        return await showTask;
    }

    private async void ContentDialog_ClosedAsync(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        await ViewModel.CancelCommandCommand.ExecuteAsync(null);
        ViewModel.Dispose();
    }

    public sealed partial class ConsoleDialogViewModel : LoggableObservableObject, IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        [ObservableProperty]
        public partial string InteractionButtonText { get; set; }

        public ConsoleDialogViewModel(string initialCloseText)
        {
            InteractionButtonText = initialCloseText;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task RunInternalAsync(string command)
        {
            CancellationToken token = _cancellationTokenSource.Token;
            await AdbConsoleViewModel.RunBuiltinCommandAsync(command, this, token);
        }

        public async Task<AdbCommandOutput> RunAdbAsync(string command)
        {
            CancellationToken token = _cancellationTokenSource.Token;
            return (await AdbManager.LoggedDeviceCommandAsync(command, this, null, true, token))!;
        }

        public void Finished(string finishedText)
        {
            InteractionButtonText = finishedText;
            WriteGenericLogLine();
            Log("You may now close this dialog.");
        }

        [RelayCommand]
        private async Task OnCancelCommandAsync()
        {
            await _cancellationTokenSource.CancelAsync();
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
        }
    }
}
