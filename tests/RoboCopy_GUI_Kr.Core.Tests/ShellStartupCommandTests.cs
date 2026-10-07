using RoboCopy_GUI_Kr.Core.Shell;
using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ShellStartupCommandTests
{
    [Fact]
    public void Parse_PreservesKoreanSpaceAndSpecialCharactersAsSinglePath()
    {
        const string path = @"C:\시험 폴더\자료 & 보고서 (최종)";

        var command = ShellStartupCommand.Parse(["--shell-set-source", path]);

        Assert.Equal(ShellStartupAction.SetSource, command.Action);
        Assert.Equal(path, command.Path);
    }

    [Theory]
    [InlineData("--shell-paste-saved", ShellStartupAction.PasteSavedSource)]
    [InlineData("--shell-paste-clipboard", ShellStartupAction.PasteClipboard)]
    public void Parse_RecognizesPasteActions(string option, ShellStartupAction expected)
    {
        var command = ShellStartupCommand.Parse([option, @"D:\대상 폴더"]);

        Assert.Equal(expected, command.Action);
    }

    [Fact]
    public void Parse_RejectsUnexpectedArgumentCount()
    {
        var command = ShellStartupCommand.Parse(["--shell-set-source", "one", "two"]);

        Assert.Equal(ShellStartupAction.None, command.Action);
        Assert.Null(command.Path);
    }

    [Theory]
    [InlineData("Copy", JobOperationMode.Copy)]
    [InlineData("Move", JobOperationMode.Move)]
    [InlineData("UpdateSync", JobOperationMode.UpdateSync)]
    [InlineData("ContinuousSync", JobOperationMode.ContinuousSync)]
    public void Parse_RecognizesExplorerPasteOperationAndPreservesTarget(string mode, JobOperationMode expected)
    {
        const string target = @"D:\한글 대상 & 폴더 (2)";

        var command = ShellStartupCommand.Parse(["--shell-paste-operation", mode, target]);

        Assert.Equal(ShellStartupAction.PasteOperation, command.Action);
        Assert.Equal(expected, command.OperationMode);
        Assert.Equal(target, command.Path);
    }

    [Theory]
    [InlineData("--shell-register-copy-menu", ShellStartupAction.RegisterCopyMenu)]
    [InlineData("--shell-unregister-menu", ShellStartupAction.UnregisterMenu)]
    public void Parse_RecognizesExplorerRegistrationCommands(string option, ShellStartupAction expected)
    {
        var command = ShellStartupCommand.Parse([option]);

        Assert.Equal(expected, command.Action);
    }

    [Fact]
    public void Parse_SetSelectionPreservesEveryExplorerArgument()
    {
        var paths = new[] { @"C:\자료\하나.txt", @"C:\자료\둘째 폴더", @"D:\다른 상위\셋.pdf" };

        var command = ShellStartupCommand.Parse(["--shell-set-selection", .. paths]);

        Assert.Equal(ShellStartupAction.SetSelection, command.Action);
        Assert.Equal(paths, command.Paths);
    }
}
