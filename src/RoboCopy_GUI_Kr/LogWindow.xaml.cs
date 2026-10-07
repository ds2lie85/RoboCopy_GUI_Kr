using System.Windows;

namespace RoboCopy_GUI_Kr;

public partial class LogWindow : Window
{
    public LogWindow(string initialText)
    {
        InitializeComponent();
        LogTextBox.Text = initialText;
        LogTextBox.CaretIndex = LogTextBox.Text.Length;
        LogTextBox.ScrollToEnd();
    }

    public void AppendText(string text, int maximumCharacters, int retainedCharacters)
    {
        LogTextBox.AppendText(text);
        if (LogTextBox.Text.Length > maximumCharacters)
        {
            var currentText = LogTextBox.Text;
            var startIndex = currentText.Length - retainedCharacters;
            var lineBreakIndex = currentText.IndexOf(Environment.NewLine, startIndex, StringComparison.Ordinal);
            startIndex = lineBreakIndex >= 0
                ? lineBreakIndex + Environment.NewLine.Length
                : startIndex;
            LogTextBox.Text = currentText[startIndex..];
        }

        LogTextBox.CaretIndex = LogTextBox.Text.Length;
        LogTextBox.ScrollToEnd();
    }

    public void ReplaceText(string text)
    {
        LogTextBox.Text = text;
        LogTextBox.CaretIndex = LogTextBox.Text.Length;
        LogTextBox.ScrollToEnd();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
