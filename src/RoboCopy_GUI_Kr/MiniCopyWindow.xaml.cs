using System.Globalization;
using System.Windows;
using System.Windows.Threading;

namespace RoboCopy_GUI_Kr;

public partial class MiniCopyWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly DispatcherTimer _refreshTimer;
    private bool _openingAdvancedSettings;
    private bool _completionHandled;

    internal MiniCopyWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _refreshTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (_, _) => Refresh(), Dispatcher);
        Closed += (_, _) =>
        {
            _refreshTimer.Stop();
            _mainWindow.NotifyMiniUiClosed();
            if (!_openingAdvancedSettings)
            {
                _mainWindow.CloseFromMiniUi();
            }
        };
        Refresh();
    }

    private void Refresh()
    {
        var state = _mainWindow.GetMiniUiState();
        var percent = Math.Clamp((int)Math.Round(state.Percent), 0, 100);
        MiniProgressBar.IsIndeterminate = state.IsIndeterminate;
        MiniProgressBar.Value = percent;
        if (!state.IsConfigured)
        {
            StateTextBlock.Text = Text("MiniUiWaitingForSettings");
            RouteTextBlock.Text = string.Empty;
            ProgressStatusTextBlock.Text = string.Empty;
            PauseResumeButton.Content = "∥";
            PauseResumeButton.ToolTip = Text("MiniUiPause");
            PauseResumeButton.IsEnabled = false;
            return;
        }

        RouteTextBlock.Text = string.Format(CultureInfo.CurrentCulture, Text("MiniUiRunningFormat"), state.SourceName, state.DestinationName, state.TotalFiles);
        StateTextBlock.Text = state.IsPaused
            ? Text("MiniUiPaused")
            : string.Format(CultureInfo.CurrentCulture, Text("MiniUiRunningStateFormat"), state.OperationName);
        ProgressStatusTextBlock.Text = state.IsPaused
            ? string.Format(CultureInfo.CurrentCulture, Text("MiniUiPausedProgressFormat"), percent, state.CompletedFiles)
            : string.Format(CultureInfo.CurrentCulture, Text("MiniUiProgressFormat"), percent, state.CompletedFiles);
        PauseResumeButton.Content = state.IsPaused ? "▶" : "∥";
        PauseResumeButton.ToolTip = state.IsPaused ? Text("MiniUiResume") : Text("MiniUiPause");
        PauseResumeButton.IsEnabled = state.CanPauseOrResume;
        if (state.IsCompleted && !_completionHandled)
        {
            _completionHandled = true;
            _refreshTimer.Stop();
            Dispatcher.BeginInvoke(Close, DispatcherPriority.Background);
        }
    }

    private void PauseResume_Click(object sender, RoutedEventArgs e) => _mainWindow.TogglePauseFromMiniUi();

    private void Stop_Click(object sender, RoutedEventArgs e) => _mainWindow.StopFromMiniUi();

    private void AdvancedSettings_Click(object sender, RoutedEventArgs e)
    {
        _openingAdvancedSettings = true;
        _mainWindow.ShowAdvancedUi();
        Close();
    }

    private string Text(string key) => FindResource(key) as string ?? key;
}
