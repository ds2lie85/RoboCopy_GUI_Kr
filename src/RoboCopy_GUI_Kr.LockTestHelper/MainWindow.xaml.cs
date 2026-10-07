using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace RoboCopy_GUI_Kr.LockTestHelper;

public partial class MainWindow : Window
{
    private static readonly TimeSpan MaximumLifetime = TimeSpan.FromMinutes(10);
    private readonly DateTimeOffset _expiresAt = DateTimeOffset.Now.Add(MaximumLifetime);
    private readonly DispatcherTimer _timer;
    private FileStream? _lockedStream;
    private bool _allowClose;

    internal bool AllowClose => _allowClose;

    public MainWindow(string lockFilePath, string sourcePath, string destinationPath)
    {
        InitializeComponent();
        _lockedStream = new FileStream(lockFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        ProcessIdText.Text = Environment.ProcessId.ToString();
        SourceFolderText.Text = sourcePath;
        DestinationFolderText.Text = destinationPath;
        LockedFileText.Text = lockFilePath;

        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, OnTimerTick, Dispatcher);
        UpdateRemainingTime();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            NotifyCloseRejected();
            return;
        }

        ReleaseLock();
        base.OnClosing(e);
    }

    private void ReleaseAndExitButton_Click(object sender, RoutedEventArgs e) => ExitSafely();

    internal void NotifyCloseRejected()
    {
        CloseStatusText.Text = (string)FindResource("CloseRejectedText");
        Activate();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (DateTimeOffset.Now >= _expiresAt)
        {
            ExitSafely();
            return;
        }

        UpdateRemainingTime();
    }

    private void UpdateRemainingTime()
    {
        TimeSpan remaining = _expiresAt - DateTimeOffset.Now;
        string format = (string)FindResource("RemainingTimeFormat");
        RemainingTimeText.Text = string.Format(format, remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining);
    }

    private void ExitSafely()
    {
        _allowClose = true;
        _timer.Stop();
        ReleaseLock();
        Close();
    }

    private void ReleaseLock()
    {
        _lockedStream?.Dispose();
        _lockedStream = null;
    }
}
