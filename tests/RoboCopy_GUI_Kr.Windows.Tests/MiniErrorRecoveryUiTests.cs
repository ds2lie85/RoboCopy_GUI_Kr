using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using RoboCopy_GUI_Kr.Core.Errors;

namespace RoboCopy_GUI_Kr.Windows.Tests;

internal static class MiniErrorRecoveryUiTests
{
    internal static string Stage { get; private set; } = "not started";
    // Called by the existing STA test, sharing its single WPF Application.
    internal static void VerifyRecovery()
    {
        foreach (var alreadyOpen in new[] { false, true })
        {
            Stage = $"constructing main (existing errors: {alreadyOpen})";
            var app = Application.Current;
            var previousMain = app.MainWindow;
            var main = new MainWindow(preferMiniUi: true) { Left = -30_000, Top = -30_000 };
            Window? mini = null;
            Window? errors = null;
            try
            {
                app.MainWindow = main;
                Set(main, "_startupInitialized", true);
                Set(main, "_miniBatchSourceDirectory", @"C:\원본 공백 & #");
                Set(main, "_miniBatchDestinationDirectory", @"C:\대상 공백 & #");
                Set(main, "_miniTotalFiles", 15155);
                Set(main, "_miniCurrentCompletedFiles", 11948);
                ((ProgressBar)main.FindName("ProgressBar")).Value = 65;
                var rows = (ObservableCollection<ErrorFileRow>)Get(main, "_errorItems")!;
                var path = typeof(RestartManagerService).Assembly.Location;
                rows.Add(new ErrorFileRow(new RobocopyErrorItem(32, RobocopyErrorCategory.Locked,
                    @"C:\원본\파일.dll", path, ""), "locked", "32", ErrorFileRetryState.Failed, "failed"));
                Stage = "showing mini";
                Invoke(main, "ShowMiniUi");
                mini = (Window)Get(main, "_miniCopyWindow")!;
                if (alreadyOpen)
                {
                    Invoke(main, "ErrorFiles_Click", main, new RoutedEventArgs());
                    errors = (Window)Get(main, "_errorFilesWindow")!;
                    Assert.Same(mini, errors.Owner);
                }
                Set(main, "_miniWorkStarted", true);
                Stage = "recovering mini";
                Invoke(mini, "Refresh");
                Stage = "verifying recovered windows";
                errors = (Window)Get(main, "_errorFilesWindow")!;
                Assert.True(main.IsVisible);
                Assert.False(mini.IsVisible);
                Assert.True(errors.IsVisible);
                Assert.Same(main, errors.Owner);
                Assert.Same(rows, ((DataGrid)errors.FindName("ErrorDataGrid")).ItemsSource);
                Assert.Equal(Visibility.Visible, ((TextBlock)errors.FindName("DiagnosisTextBlock")).Visibility);
                Assert.Null(Get(main, "_miniCopyWindow"));
                Assert.Equal(ShutdownMode.OnMainWindowClose, app.ShutdownMode);
            }
            finally
            {
                Stage = "closing test windows";
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                errors?.Close();
                // A failed assertion before recovery must not invoke mini's app shutdown.
                if (mini?.IsVisible == true) Set(mini, "_openingAdvancedSettings", true);
                mini?.Close();
                main.Close();
                app.MainWindow = previousMain;
            }
        }
        Stage = "complete";
    }

    private static object? Get(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    private static void Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
}
