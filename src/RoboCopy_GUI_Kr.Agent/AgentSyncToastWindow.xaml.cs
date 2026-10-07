using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using RoboCopy_GUI_Kr.Core.ContinuousSync;

namespace RoboCopy_GUI_Kr.Agent;

public partial class AgentSyncToastWindow : Window
{
    private readonly ContinuousSyncNotification _notification;
    private readonly Action<ContinuousSyncNotification> _openDetails;
    private readonly DispatcherTimer _closeTimer;

    public AgentSyncToastWindow(ContinuousSyncNotification notification, string message, Action<ContinuousSyncNotification> openDetails)
    {
        InitializeComponent();
        _notification = notification;
        _openDetails = openDetails;
        MessageTextBlock.Text = message;
        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _closeTimer.Tick += (_, _) => Close();
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 14;
            Top = area.Bottom - ActualHeight - 14;
            _closeTimer.Start();
        };
        Closed += (_, _) => _closeTimer.Stop();
    }

    private void Toast_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _closeTimer.Stop();
        Close();
        _openDetails(_notification);
    }
}
