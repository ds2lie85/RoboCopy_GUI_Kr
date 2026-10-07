using System.IO;
using System.Windows;

namespace RoboCopy_GUI_Kr;

public partial class ExplorerIntegrationWindow : Window
{
    private readonly ExplorerContextMenuService _service = new();

    public ExplorerIntegrationWindow()
    {
        InitializeComponent();
        RefreshStatus();
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _service.RegisterCopyMenu();
            RefreshStatus();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            MessageBox.Show(this, exception.Message, GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Unregister_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, GetText("ExplorerUnregisterPrompt"), GetText("ExplorerIntegration"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _service.Unregister();
            RefreshStatus();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            MessageBox.Show(this, exception.Message, GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshStatus() => StatusTextBlock.Text = GetText(!_service.IsRegistered
        ? "ExplorerIntegrationNotRegistered"
        : _service.IsPasteMenuActive
            ? "ExplorerIntegrationPasteReady"
            : "ExplorerIntegrationCopyReady");

    private string GetText(string key) => FindResource(key) as string ?? key;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
