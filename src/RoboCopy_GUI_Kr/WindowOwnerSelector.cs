using System.Windows;

namespace RoboCopy_GUI_Kr;

internal static class WindowOwnerSelector
{
    internal static Window? Select(Window mainWindow, Window? miniWindow) =>
        mainWindow.IsVisible ? mainWindow : miniWindow is { IsVisible: true } ? miniWindow : null;
}
