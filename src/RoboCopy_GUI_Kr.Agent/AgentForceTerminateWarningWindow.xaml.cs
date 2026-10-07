using System.Windows;

namespace RoboCopy_GUI_Kr.Agent;

public partial class AgentForceTerminateWarningWindow : Window
{
    public AgentForceTerminateWarningWindow() => InitializeComponent();

    private void Proceed_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
