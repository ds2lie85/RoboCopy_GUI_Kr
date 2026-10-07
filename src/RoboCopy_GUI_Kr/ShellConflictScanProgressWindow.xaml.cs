using System.Globalization;
using System.Windows;
using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr;

public partial class ShellConflictScanProgressWindow : Window
{
    public ShellConflictScanProgressWindow()
    {
        InitializeComponent();
        ScanStatusTextBlock.Text = GetText("ShellConflictCollecting");
    }

    public void Report(ShellConflictScanProgress progress)
    {
        if (progress.Stage is ShellConflictScanStage.CollectingFiles or ShellConflictScanStage.ScanningDestinations)
        {
            ScanProgressBar.IsIndeterminate = true;
            ScanStatusTextBlock.Text = string.Format(
                CultureInfo.CurrentCulture,
                GetText(progress.Stage == ShellConflictScanStage.CollectingFiles
                    ? "ShellConflictCollectedFormat"
                    : "ShellConflictDestinationScannedFormat"),
                progress.Completed);
            return;
        }

        ScanProgressBar.IsIndeterminate = false;
        ScanProgressBar.Maximum = Math.Max(1, progress.Total);
        ScanProgressBar.Value = progress.Completed;
        ScanStatusTextBlock.Text = string.Format(
            CultureInfo.CurrentCulture,
            GetText("ShellConflictComparingFormat"),
            progress.Completed,
            progress.Total);
    }

    private string GetText(string key) => FindResource(key) as string ?? key;
}
