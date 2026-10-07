using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Shell;

public enum ShellStartupAction
{
    None,
    RegisterCopyMenu,
    UnregisterMenu,
    SetSelection,
    SetSource,
    PasteOperation,
    PasteSavedSource,
    PasteClipboard,
}

public sealed record ShellStartupCommand(
    ShellStartupAction Action,
    string? Path,
    JobOperationMode? OperationMode = null)
{
    public IReadOnlyList<string> Paths { get; init; } = [];

    public static ShellStartupCommand Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count >= 2 && arguments[0] == "--shell-set-selection")
        {
            var paths = arguments.Skip(1).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
            return paths.Length == 0
                ? new ShellStartupCommand(ShellStartupAction.None, null)
                : new ShellStartupCommand(ShellStartupAction.SetSelection, paths[0]) { Paths = paths };
        }

        if (arguments.Count == 1)
        {
            return arguments[0] switch
            {
                "--shell-register-copy-menu" => new(ShellStartupAction.RegisterCopyMenu, null),
                "--shell-unregister-menu" => new(ShellStartupAction.UnregisterMenu, null),
                _ => new(ShellStartupAction.None, null),
            };
        }

        if (arguments.Count == 3
            && arguments[0] == "--shell-paste-operation"
            && Enum.TryParse<JobOperationMode>(arguments[1], ignoreCase: false, out var operationMode)
            && !string.IsNullOrWhiteSpace(arguments[2]))
        {
            return new ShellStartupCommand(ShellStartupAction.PasteOperation, arguments[2], operationMode);
        }

        if (arguments.Count != 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return new ShellStartupCommand(ShellStartupAction.None, null);
        }

        var action = arguments[0] switch
        {
            "--shell-set-source" => ShellStartupAction.SetSource,
            "--shell-paste-saved" => ShellStartupAction.PasteSavedSource,
            "--shell-paste-clipboard" => ShellStartupAction.PasteClipboard,
            _ => ShellStartupAction.None,
        };

        return action == ShellStartupAction.None
            ? new ShellStartupCommand(action, null)
            : new ShellStartupCommand(action, arguments[1]);
    }
}
