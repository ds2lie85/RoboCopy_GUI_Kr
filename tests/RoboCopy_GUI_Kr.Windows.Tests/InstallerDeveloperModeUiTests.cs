using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RoboCopy_GUI_Kr.Windows.Tests;

public sealed class InstallerDeveloperModeUiTests
{
    [Fact]
    public void Installer_DefaultsUncheckedAndRendersPurposeAndWarning()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
                var script = File.ReadAllText(Path.Combine(fixtures, "Install.ps1"));
                var xaml = Regex.Match(script, "\\[xml\\]\\$xaml = @\"\\r?\\n(.*?)\\r?\\n\"@", RegexOptions.Singleline).Groups[1].Value;
                Assert.NotEmpty(xaml);
                var window = (Window)XamlReader.Parse(xaml);
                try
                {
                    var checkbox = (CheckBox)window.FindName("DeveloperModeCheckBox");
                    Assert.False(checkbox.IsChecked);
                    var resources = File.ReadAllText(Path.Combine(fixtures, "InstallerStrings.ko-KR.psd1"));
                    string Text(string key) => Regex.Match(resources, Regex.Escape(key) + " = '([^']*)'").Groups[1].Value;
                    checkbox.Content = Text("DeveloperModeOption");
                    var warning = (TextBlock)window.FindName("DeveloperModeWarningText");
                    warning.Text = Text("DeveloperModeWarning");
                    Assert.Contains("Windows 11", checkbox.Content.ToString());
                    Assert.Contains("기본 우클릭 메뉴", warning.Text);
                    Assert.Contains("예기치 못한 위험", warning.Text);
                    Assert.Contains("PC 전체", warning.Text);
                    var content = (FrameworkElement)window.Content;
                    ((Grid)content).Background = window.Background;
                    ((TextBlock)window.FindName("WindowsStatusText")).Text = "✓ Windows x64 — 지원됨";
                    ((TextBlock)window.FindName("RobocopyStatusText")).Text = "✓ Windows Robocopy — 사용 가능";
                    ((TextBlock)window.FindName("InternetStatusText")).Text = "• 설치 환경 검사 결과";
                    var size = new Size(712, 700);
                    content.Measure(size); content.Arrange(new Rect(size)); content.UpdateLayout();
                    Assert.True(warning.ActualHeight > 30, "Warning must wrap visibly.");
                    Assert.True(warning.TranslatePoint(new Point(0, warning.ActualHeight), content).Y < size.Height - 30,
                        "Warning must fit above the installer buttons.");
                    var bitmap = new RenderTargetBitmap(712, 700, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(Path.Combine(AppContext.BaseDirectory, "InstallerDeveloperMode-preview.png"));
                    encoder.Save(output);
                }
                finally { window.Close(); }
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Installer layout test timed out.");
        Assert.Null(failure);
    }
}
