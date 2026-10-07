using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RoboCopy_GUI_Kr.Core.ContinuousSync;

namespace RoboCopy_GUI_Kr.Agent;

public partial class AgentNotificationDetailWindow : Window
{
    private sealed record Row(string Action, string RelativePath, string FullPath);

    public static AgentNotificationDetailWindow Open(
        ContinuousSyncNotification notification,
        Window? owner = null)
    {
        var window = new AgentNotificationDetailWindow(notification);
        if (owner?.IsVisible == true)
        {
            window.Owner = owner;
        }

        window.Show();
        window.Activate();
        return window;
    }

    public AgentNotificationDetailWindow(ContinuousSyncNotification notification)
    {
        InitializeComponent();
        SummaryTextBlock.Text = string.Format(
            CultureInfo.CurrentCulture,
            FindResource("AgentNotificationFormat") as string ?? string.Empty,
            RepresentativeName(notification), notification.CopiedFiles + notification.DeletedFiles,
            notification.CopiedFiles, notification.DeletedFiles);
        ChangeDataGrid.ItemsSource = notification.Changes.Select(item => new Row(
            FindResource(item.Kind == ContinuousSyncChangeKind.Copied ? "AgentNotificationCopied" : "AgentNotificationDeleted") as string ?? item.Kind.ToString(),
            item.RelativePath,
            Path.Combine(notification.DestinationDirectory, item.RelativePath))).ToArray();
    }

    private void ChangeDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row is not null) ChangeDataGrid.SelectedItem = row.Item;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (ChangeDataGrid.SelectedItem is Row row && File.Exists(row.FullPath))
        {
            if (TryOpen(row.FullPath)) return;
        }
        ShowOpenFailure();
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (ChangeDataGrid.SelectedItem is not Row row) return;
        var directory = Path.GetDirectoryName(row.FullPath);
        if (Directory.Exists(directory))
        {
            if (TryOpen(directory)) return;
        }
        ShowOpenFailure();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static bool TryOpen(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return false;
        }
    }

    private void ShowOpenFailure() => System.Windows.MessageBox.Show(
        this,
        FindResource("AgentNotificationOpenFailed") as string ?? string.Empty,
        FindResource("AgentNotificationDetailTitle") as string ?? string.Empty,
        MessageBoxButton.OK,
        MessageBoxImage.Information);

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T value) return value;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private string RepresentativeName(ContinuousSyncNotification item) =>
        item.Changes.FirstOrDefault() is { RelativePath: { Length: > 0 } path }
            ? Path.GetFileName(path)
            : FindResource("AgentNotificationUnknownFile") as string ?? string.Empty;
}
