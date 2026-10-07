using System.Windows;

namespace RoboCopy_GUI_Kr;

internal enum ShellActionDecision
{
    Cancel,
    StartWithDefaults,
    Configure,
}

public partial class ShellActionPromptWindow : Window
{
    public ShellActionPromptWindow()
    {
        InitializeComponent();
    }

    internal ShellActionDecision Decision { get; private set; } = ShellActionDecision.Cancel;

    private void StartWithDefaults_Click(object sender, RoutedEventArgs e)
    {
        Decision = ShellActionDecision.StartWithDefaults;
        DialogResult = true;
    }

    private void Configure_Click(object sender, RoutedEventArgs e)
    {
        Decision = ShellActionDecision.Configure;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Decision = ShellActionDecision.Cancel;
        DialogResult = false;
    }
}
