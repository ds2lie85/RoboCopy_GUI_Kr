using Microsoft.Win32;
using RoboCopy_GUI_Kr.Core.ContinuousSync;

namespace RoboCopy_GUI_Kr.Agent;

public static class AgentStartupRegistration
{
    private const string ValueName = "RoboCopy_GUI_Kr_Agent";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Register(string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("The Windows startup registry key could not be opened.");
        key.SetValue(
            ValueName,
            ContinuousSyncAgentCommand.BuildStartupCommand(executablePath),
            RegistryValueKind.String);
    }

    public static void Unregister()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
