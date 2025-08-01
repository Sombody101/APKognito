using APKognito.Utilities.MVVM;
using APKognito.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace APKognito.Controls.Dialogs;

public sealed partial class ConsoleDialog
{
    public ConsoleDialogViewModel ViewModel { get; }

    public ConsoleDialog(string command, ContentPresenter? contentPresenter)
        : base(contentPresenter)
    {
        ViewModel = new ConsoleDialogViewModel(command);
        DataContext = this;

        InitializeComponent();
    }

    protected override void OnLoaded()
    {
        _ = ViewModel.StartCommandAsync();
        base.OnLoaded();
    }

    private async void ContentDialog_ClosedAsync(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        await ViewModel.CancelCommandCommand.ExecuteAsync(null);
        ViewModel.Dispose();
    }

    public sealed partial class ConsoleDialogViewModel : LoggableObservableObject, IDisposable
    {
        private readonly string _command;

        private readonly CancellationTokenSource _cancellationTokenSource;

        [ObservableProperty]
        public partial string InteractionButtonText { get; set; } = "Cancel install";

        public ConsoleDialogViewModel(string command)
        {
            _command = command;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartCommandAsync()
        {
            CancellationToken token = _cancellationTokenSource.Token;
            await Task.Run(async () =>
            await AdbConsoleViewModel.RunBuiltinCommandAsync(_command, this, token), token);

            InteractionButtonText = "Close";

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
