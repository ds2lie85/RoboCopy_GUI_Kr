using System.Windows;

namespace RoboCopy_GUI_Kr;

public partial class ProfileNameWindow : Window
{
    public ProfileNameWindow(string initialName = "")
    {
        InitializeComponent();
        ProfileNameTextBox.Text = initialName;
        Loaded += (_, _) =>
        {
            ProfileNameTextBox.Focus();
            ProfileNameTextBox.SelectAll();
        };
    }

    public string ProfileName => ProfileNameTextBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileName.Length == 0)
        {
            MessageBox.Show(this, FindResource("ProfileNameRequired") as string, Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}

