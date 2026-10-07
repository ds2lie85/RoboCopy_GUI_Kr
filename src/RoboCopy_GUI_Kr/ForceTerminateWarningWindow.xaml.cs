using System.Windows;

namespace RoboCopy_GUI_Kr;

public partial class ForceTerminateWarningWindow : Window
{
    public ForceTerminateWarningWindow() => InitializeComponent();

    private void Proceed_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
