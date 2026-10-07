using System.IO;
using System.Windows;

namespace RoboCopy_GUI_Kr.LockTestHelper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string testRoot = Path.Combine(Path.GetTempPath(), "RoboCopy_GUI_Kr_ForceTerminateTest");
        string lockFilePath = e.Args.Length > 0
            ? Path.GetFullPath(e.Args[0])
            : Path.Combine(testRoot, "source", "잠금 시험 파일.txt");
        string sourcePath = Path.GetDirectoryName(lockFilePath)!;
        string destinationPath = Path.Combine(Path.GetDirectoryName(sourcePath)!, "destination");

        Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);
        Directory.CreateDirectory(destinationPath);
        File.WriteAllText(lockFilePath, "RoboCopy GUI 강제 종료 기능을 검증하기 위한 일회용 파일입니다.");

        var window = new MainWindow(lockFilePath, sourcePath, destinationPath);
        MainWindow = window;
        window.Show();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        if (MainWindow is MainWindow window && !window.AllowClose)
        {
            e.Cancel = true;
            window.NotifyCloseRejected();
            return;
        }

        base.OnSessionEnding(e);
    }
}
