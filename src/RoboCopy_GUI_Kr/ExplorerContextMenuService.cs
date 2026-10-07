using Microsoft.Win32;
using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr;

internal sealed class ExplorerContextMenuService
{
    internal const string SelectionHandlerClassId = "{9A59C322-8A2D-4D57-B10D-39971ED3DF78}";
    private const string Root = @"Software\Classes";
    private const string ClassServerKey = @"CLSID\" + SelectionHandlerClassId + @"\LocalServer32";
    private const string CopySelectionKey = @"AllFilesystemObjects\shell\RoboCopyGUIKr.Copy";
    private const string PasteDirectoryKey = @"Directory\shell\RoboCopyGUIKr.Paste";
    private const string PasteBackgroundKey = @"Directory\Background\shell\RoboCopyGUIKr.Paste";

    public bool IsRegistered => IsCurrentExecutableComServer()
        || IsCurrentExecutablePasteMenu(PasteDirectoryKey)
        || IsCurrentExecutablePasteMenu(PasteBackgroundKey);

    public bool IsPasteMenuActive
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{Root}\{PasteDirectoryKey}");
            return key is not null;
        }
    }

    public void RegisterCopyMenu()
    {
        Unregister();
        CreateCopySelectionVerb(GetExecutablePath());
    }

    public void ShowPasteMenu()
    {
        var executablePath = GetExecutablePath();
        CreateCopySelectionVerb(executablePath);
        CreatePasteCascade(PasteDirectoryKey, executablePath, "%1");
        CreatePasteCascade(PasteBackgroundKey, executablePath, "%V");
    }

    public void Unregister()
    {
        foreach (var key in Keys.Concat(LegacyKeys))
        {
            DeleteKey(key);
        }
    }

    internal static string BuildCommand(string executablePath, string option, params string[] arguments)
    {
        var quotedArguments = arguments.Select(argument => $"\"{argument}\"");
        return $"\"{executablePath.Replace("\"", "\\\"")}\" {option} {string.Join(' ', quotedArguments)}";
    }

    internal static string BuildSelectionCommand(string executablePath) =>
        $"\"{executablePath.Replace("\"", "\\\"")}\" --shell-set-selection \"%1\"";

    private static readonly string[] Keys = [CopySelectionKey, PasteDirectoryKey, PasteBackgroundKey, ClassServerKey];

    private static readonly string[] LegacyKeys =
    [
        @"Directory\shell\RoboCopyGUIKr.Copy",
        @"Directory\shell\RoboCopyGUIKr.SetSource",
        @"Directory\shell\RoboCopyGUIKr.PasteSaved",
        @"Directory\Background\shell\RoboCopyGUIKr.PasteSaved",
        @"Directory\shell\RoboCopyGUIKr.PasteClipboard",
        @"Directory\Background\shell\RoboCopyGUIKr.PasteClipboard",
    ];

    private static string GetExecutablePath() => Environment.ProcessPath
        ?? throw new InvalidOperationException("Executable path is unavailable.");

    private static void CreateCopySelectionVerb(string executablePath)
    {
        using (var server = Registry.CurrentUser.CreateSubKey($@"{Root}\{ClassServerKey}", writable: true))
        {
            server.SetValue(null, BuildCommand(executablePath, "--shell-com-server"));
        }

        using var verb = Registry.CurrentUser.CreateSubKey($@"{Root}\{CopySelectionKey}", writable: true);
        verb.SetValue(null, "Robocopy로 복사");
        verb.SetValue("Icon", executablePath);
        verb.SetValue("MultiSelectModel", "Player");
        verb.SetValue("Position", "Top");
        using var command = verb.CreateSubKey("command", writable: true);
        command.SetValue(null, string.Empty);
        command.SetValue("DelegateExecute", SelectionHandlerClassId);
    }

    private static void CreatePasteCascade(string relativeKey, string executablePath, string placeholder)
    {
        using var menu = Registry.CurrentUser.CreateSubKey($@"{Root}\{relativeKey}", writable: true);
        menu.SetValue("MUIVerb", "Robocopy로 붙여넣기");
        menu.SetValue("Icon", executablePath);
        menu.SetValue("SubCommands", string.Empty);
        menu.SetValue("Position", "Top");

        CreateSubVerb(menu, "01.Copy", "붙여넣기", executablePath, JobOperationMode.Copy, placeholder);
        CreateSubVerb(menu, "02.Move", "이동", executablePath, JobOperationMode.Move, placeholder);
        CreateSubVerb(menu, "03.UpdateSync", "단방향 동기화(백업)", executablePath, JobOperationMode.UpdateSync, placeholder);
        CreateSubVerb(menu, "04.ContinuousSync", "단방향 동기화(지속)", executablePath, JobOperationMode.ContinuousSync, placeholder);
    }

    private static void CreateSubVerb(
        RegistryKey parent,
        string keyName,
        string caption,
        string executablePath,
        JobOperationMode operationMode,
        string placeholder)
    {
        using var verb = parent.CreateSubKey($@"shell\{keyName}", writable: true);
        verb.SetValue("MUIVerb", caption);
        using var command = verb.CreateSubKey("command", writable: true);
        command.SetValue(null, BuildCommand(executablePath, "--shell-paste-operation", operationMode.ToString(), placeholder));
    }

    private static bool IsCurrentExecutableCommand(string relativeKey)
    {
        using var command = Registry.CurrentUser.OpenSubKey($@"{Root}\{relativeKey}\command");
        return ContainsCurrentExecutable(command?.GetValue(null) as string);
    }

    private static bool IsCurrentExecutableComServer()
    {
        using var server = Registry.CurrentUser.OpenSubKey($@"{Root}\{ClassServerKey}");
        return ContainsCurrentExecutable(server?.GetValue(null) as string);
    }

    private static bool IsCurrentExecutablePasteMenu(string relativeKey)
    {
        using var command = Registry.CurrentUser.OpenSubKey($@"{Root}\{relativeKey}\shell\01.Copy\command");
        return ContainsCurrentExecutable(command?.GetValue(null) as string);
    }

    private static bool ContainsCurrentExecutable(string? command)
    {
        var executablePath = Environment.ProcessPath;
        return !string.IsNullOrWhiteSpace(executablePath)
            && command?.Contains(executablePath, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void DeleteKey(string relativeKey)
    {
        using var classes = Registry.CurrentUser.OpenSubKey(Root, writable: true);
        classes?.DeleteSubKeyTree(relativeKey, throwOnMissingSubKey: false);
    }
}
