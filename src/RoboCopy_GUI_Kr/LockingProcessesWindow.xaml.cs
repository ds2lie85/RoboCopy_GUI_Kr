using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using RoboCopy_GUI_Kr.Core.Errors;

namespace RoboCopy_GUI_Kr;

public partial class LockingProcessesWindow : Window
{
    private readonly RestartManagerService _restartManager = new();
    private readonly IReadOnlyList<string> _paths;
    private IReadOnlyList<LockingProcessInfo> _processes;

    public LockingProcessesWindow(IReadOnlyList<LockingProcessInfo> processes, IReadOnlyList<string> paths)
    {
        InitializeComponent();
        _processes = processes;
        _paths = paths;
        DataContext = _processes;
        RequestShutdownButton.IsEnabled = !_processes.Any(process => process.ProcessId == Environment.ProcessId);
    }

    public bool RetryRequested { get; private set; }

    private async void RequestShutdown_Click(object sender, RoutedEventArgs e)
    {
        if (_processes.Any(process => process.ProcessId == Environment.ProcessId))
        {
            MessageBox.Show(this, GetText("SelfOverwriteGuidance"), GetText("LockingProcessesTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var names = string.Join(", ", _processes.Select(process =>
            string.IsNullOrWhiteSpace(process.ApplicationName)
                ? $"PID {process.ProcessId}"
                : $"{process.ApplicationName} (PID {process.ProcessId})"));
        var prompt = string.Format(
            CultureInfo.CurrentCulture,
            GetText("NormalShutdownPromptFormat"),
            names,
            Environment.NewLine);
        if (MessageBox.Show(
                this,
                prompt,
                GetText("NormalShutdownTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        RequestShutdownButton.IsEnabled = false;
        try
        {
            await Task.Run(() => _restartManager.RequestGracefulShutdown(_paths));
            await Task.Delay(500);
            _processes = await Task.Run(() => _restartManager.GetLockingProcesses(_paths));
            DataContext = _processes;
            if (_processes.Count == 0)
            {
                RetryRequested = true;
                MessageBox.Show(
                    this,
                    GetText("NormalShutdownRetryQueued"),
                    GetText("NormalShutdownTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Close();
                return;
            }

            MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, GetText("NormalShutdownResultFormat"), _processes.Count),
                GetText("NormalShutdownTitle"),
                MessageBoxButton.OK,
                _processes.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            SetForceTerminateVisible(_processes.Count > 0);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException)
        {
            SetForceTerminateVisible(true);
            MessageBox.Show(
                this,
                string.Format(CultureInfo.CurrentCulture, GetText("NormalShutdownFailedFormat"), exception.Message),
                GetText("NormalShutdownTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            RequestShutdownButton.IsEnabled = _processes.Count > 0
                && !_processes.Any(process => process.ProcessId == Environment.ProcessId);
        }
    }

    private async void ForceTerminate_Click(object sender, RoutedEventArgs e)
    {
        var warning = new ForceTerminateWarningWindow { Owner = this };
        if (warning.ShowDialog() != true)
        {
            return;
        }

        ForceTerminateButton.IsEnabled = false;
        ForceTerminateMenuItem.IsEnabled = false;
        try
        {
            var result = await Task.Run(() => _restartManager.ForceTerminateLockingProcesses(_paths));
            _processes = result.RemainingProcesses;
            DataContext = _processes;
            if (_processes.Count == 0)
            {
                RetryRequested = true;
                MessageBox.Show(this, GetText("ForceTerminateRetryQueued"), GetText("ForceTerminateTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
                return;
            }

            MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, GetText("ForceTerminatePartialFormat"), _processes.Count),
                GetText("ForceTerminateTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, GetText("ForceTerminateFailedFormat"), exception.Message),
                GetText("ForceTerminateTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ForceTerminateButton.IsEnabled = true;
            ForceTerminateMenuItem.IsEnabled = true;
        }
    }

    private void SetForceTerminateVisible(bool visible)
    {
        var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ForceTerminateButton.Visibility = visibility;
        ForceTerminateMenuItem.Visibility = visibility;
    }

    private string GetText(string resourceKey) => FindResource(resourceKey) as string ?? resourceKey;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
