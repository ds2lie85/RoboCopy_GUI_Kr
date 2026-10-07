using System.Windows;

namespace RoboCopy_GUI_Kr;

public partial class MoveFailureDecisionWindow : Window
{
    public MoveFailureDecisionWindow(int errorCount)
    {
        InitializeComponent();
        var format = FindResource("MoveErrorPolicyDescriptionFormat") as string;
        if (!string.IsNullOrWhiteSpace(format))
        {
            DescriptionTextBlock.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, format, errorCount);
        }
    }

    public MoveFailureDecision Decision { get; private set; } = MoveFailureDecision.Stop;

    private void MoveFirst_Click(object sender, RoutedEventArgs e) => CloseWith(MoveFailureDecision.MoveVerifiedItemsFirst);

    private void Defer_Click(object sender, RoutedEventArgs e) => CloseWith(MoveFailureDecision.DeferUntilAllResolved);

    private void Stop_Click(object sender, RoutedEventArgs e) => CloseWith(MoveFailureDecision.Stop);

    private void Delete_Click(object sender, RoutedEventArgs e) => CloseWith(MoveFailureDecision.DeleteCopiedFiles);

    private void CloseWith(MoveFailureDecision decision)
    {
        Decision = decision;
        DialogResult = true;
    }
}
