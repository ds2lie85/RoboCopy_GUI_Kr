using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using RoboCopy_GUI_Kr.Core.ContinuousSync;

namespace RoboCopy_GUI_Kr.Agent;

public partial class AgentNotificationListWindow : Window
{
    private sealed record Row(ContinuousSyncNotification Notification, string CompletedAt, string Target, string Summary);

    public AgentNotificationListWindow(IReadOnlyList<ContinuousSyncNotification> notifications)
    {
        InitializeComponent();
        var rows = notifications.Select(item => new Row(
            item,
            item.CreatedAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture),
            string.Format(
                CultureInfo.CurrentCulture,
                FindResource("AgentNotificationTargetFormat") as string ?? string.Empty,
                FolderName(item.SourceDirectory),
                FolderName(item.DestinationDirectory)),
            FormatSummary(item))).ToArray();
        NotificationDataGrid.ItemsSource = rows;
        EmptyTextBlock.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NotificationDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NotificationDataGrid.SelectedItem is Row row)
        {
            AgentNotificationDetailWindow.Open(row.Notification, this);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private string FormatSummary(ContinuousSyncNotification item) => string.Format(
        CultureInfo.CurrentCulture,
        FindResource("AgentNotificationFormat") as string ?? string.Empty,
        RepresentativeName(item), item.CopiedFiles + item.DeletedFiles, item.CopiedFiles, item.DeletedFiles);

    private string RepresentativeName(ContinuousSyncNotification item) =>
        item.Changes.FirstOrDefault() is { RelativePath: { Length: > 0 } path }
            ? Path.GetFileName(path)
            : FindResource("AgentNotificationUnknownFile") as string ?? string.Empty;

    private static string FolderName(string path) => Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } name ? name : path;
}
