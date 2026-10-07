using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using RoboCopy_GUI_Kr.Core.Errors;

namespace RoboCopy_GUI_Kr.Agent;

public sealed record AgentErrorRow(RobocopyErrorItem Item, string CategoryText)
{
    public int ErrorCode => Item.ErrorCode;
    public string ErrorDescription => $"{RobocopyErrorDescription.GetSystemMessage(Item.ErrorCode)}{Environment.NewLine}{Item.Message}";
}

public partial class AgentErrorWindow : Window
{
    private readonly RestartManagerService _restartManager = new();
    private readonly AgentElevatedWorkerClient _elevatedWorker = new();
    private readonly AgentErrorRow[] _rows;
    private IReadOnlyList<string> _forceTerminationPaths = [];

    public AgentErrorWindow(IEnumerable<RobocopyErrorItem> items)
    {
        InitializeComponent();
        _rows = items.Select(item => new AgentErrorRow(item, Text(item.Category switch
        {
            RobocopyErrorCategory.AccessDenied => "ErrorAccessDenied",
            RobocopyErrorCategory.Locked => "ErrorLocked",
            RobocopyErrorCategory.PathNotFound => "ErrorPath",
            RobocopyErrorCategory.DiskFull => "ErrorDisk",
            RobocopyErrorCategory.NetworkError => "ErrorNetwork",
            RobocopyErrorCategory.FileChanged => "ErrorChanged",
            _ => "ErrorUnknown",
        }))).ToArray();
        ErrorsDataGrid.ItemsSource = _rows;
        Loaded += (_, _) => ErrorsDataGrid.SelectAll();
    }

    public bool RetryRequested { get; private set; }

    private AgentErrorRow[] SelectedRows() => ErrorsDataGrid.SelectedItems.Cast<AgentErrorRow>().ToArray();

    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        RetryRequested = true;
        Close();
    }

    private void CheckLocks_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var processes = _restartManager.GetLockingProcesses(SelectedPaths());
            var message = processes.Count == 0
                ? Text("NoLockingProcesses")
                : string.Join(Environment.NewLine, processes.Select(process => $"{process.ApplicationName} (PID {process.ProcessId})"));
            System.Windows.MessageBox.Show(this, message, Text("AgentErrorsTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException)
        {
            ShowFailure(exception.Message);
        }
    }

    private async void GracefulRetry_Click(object sender, RoutedEventArgs e)
    {
        var paths = SelectedPaths();
        if (paths.Length == 0)
        {
            ShowFailure(Text("SelectErrors"));
            return;
        }

        if (System.Windows.MessageBox.Show(this, Text("GracefulShutdownConfirm"), Text("AgentErrorsTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await Task.Run(() => _restartManager.RequestGracefulShutdown(paths));
            await Task.Delay(500);
            var remaining = await Task.Run(() => _restartManager.GetLockingProcesses(paths));
            if (remaining.Count == 0)
            {
                RetryRequested = true;
                Close();
                return;
            }

            _forceTerminationPaths = paths;
            SetForceTerminateVisible(true);
            ShowFailure(string.Format(CultureInfo.CurrentCulture, Text("GracefulShutdownRemainingFormat"), remaining.Count));
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException)
        {
            _forceTerminationPaths = paths;
            SetForceTerminateVisible(true);
            ShowFailure(exception.Message);
        }
    }

    private async void ForceTerminate_Click(object sender, RoutedEventArgs e)
    {
        if (_forceTerminationPaths.Count == 0)
        {
            return;
        }

        var warning = new AgentForceTerminateWarningWindow { Owner = this };
        if (warning.ShowDialog() != true)
        {
            return;
        }

        ForceTerminateButton.IsEnabled = false;
        ForceTerminateMenuItem.IsEnabled = false;
        try
        {
            var result = await Task.Run(() => _restartManager.ForceTerminateLockingProcesses(_forceTerminationPaths));
            if (result.RemainingProcesses.Count == 0)
            {
                RetryRequested = true;
                Close();
                return;
            }

            ShowFailure(string.Format(CultureInfo.CurrentCulture, Text("ForceTerminatePartialFormat"), result.RemainingProcesses.Count));
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowFailure(exception.Message);
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

    private async void ElevatedRetry_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedRows()
            .Where(row => row.Item.Category == RobocopyErrorCategory.AccessDenied
                          && (File.Exists(row.Item.SourcePath) || Directory.Exists(row.Item.SourcePath)))
            .ToArray();
        if (selected.Length == 0)
        {
            ShowFailure(Text("SelectAccessDenied"));
            return;
        }

        var requestRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RoboCopy_GUI_Kr", "elevated-requests", Guid.NewGuid().ToString("N"));
        try
        {
            var result = await _elevatedWorker.RunAsync(new ElevatedRetryRequest
            {
                Files = selected.Select(row => new ElevatedRetryFile(row.Item.SourcePath, row.Item.DestinationPath)).ToArray(),
                ResultPath = Path.Combine(requestRoot, "result.json"),
                RetryCount = 0,
                RetryWaitSeconds = 0,
                Restartable = true,
            });
            if (result.WasCanceled)
            {
                ShowFailure(Text("ElevatedCanceled"));
                return;
            }

            if (result.Result?.Files.All(file => file.IsSuccess) == true)
            {
                RetryRequested = true;
                Close();
                return;
            }

            ShowFailure(Text("ElevatedFailed"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or Win32Exception)
        {
            ShowFailure(exception.Message);
        }
    }

    private string[] SelectedPaths() => SelectedRows()
        .SelectMany(row => new[] { row.Item.SourcePath, row.Item.DestinationPath })
        .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private void ShowFailure(string message) => System.Windows.MessageBox.Show(
        this,
        string.Format(CultureInfo.CurrentCulture, Text("AgentErrorActionFailedFormat"), message),
        Text("AgentErrorsTitle"),
        MessageBoxButton.OK,
        MessageBoxImage.Warning);

    private string Text(string key) => FindResource(key) as string ?? key;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
