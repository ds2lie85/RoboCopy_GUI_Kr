using System.IO;
using System.Windows;
using Microsoft.Win32;
using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Paths;
using RoboCopy_GUI_Kr.Core.Validation;

namespace RoboCopy_GUI_Kr;

public partial class QueueJobEditorWindow : Window
{
    private readonly RobocopyJobOptions _baseOptions;

    public QueueJobEditorWindow(RobocopyJobOptions options)
    {
        InitializeComponent();
        _baseOptions = options;
        SourceTextBox.Text = options.SourceDirectory;
        DestinationTextBox.Text = options.DestinationDirectory;
    }

    public RobocopyJobOptions? ResultOptions { get; private set; }

    private void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        BrowseInto(SourceTextBox, GetText("SourceFolder"), SourceTextBox.Text);
    }

    private void BrowseDestination_Click(object sender, RoutedEventArgs e)
    {
        BrowseInto(
            DestinationTextBox,
            GetText("DestinationFolder"),
            FolderDialogStartPath.ResolveDestinationStartPath(SourceTextBox.Text));
    }

    private void BrowseInto(System.Windows.Controls.TextBox target, string title, string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : null,
        };

        if (dialog.ShowDialog(this) == true)
        {
            target.Text = dialog.FolderName;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var errors = PathSafetyValidator.ValidateCopyDirectories(SourceTextBox.Text, DestinationTextBox.Text);
        if (errors.Count != 0)
        {
            var message = string.Join(Environment.NewLine, errors.Select(GetText));
            MessageBox.Show(this, message, GetText("ErrorsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultOptions = _baseOptions with
        {
            SourceDirectory = SourceTextBox.Text,
            DestinationDirectory = DestinationTextBox.Text,
        };
        DialogResult = true;
    }

    private string GetText(string resourceKey)
    {
        return FindResource(resourceKey) as string ?? resourceKey;
    }
}
