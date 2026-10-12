using System.Runtime.InteropServices;
using System.IO;
using System.Xml.Linq;
using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr.Windows.Tests;

public sealed class ShellExplorerCommandTests
{
    [Theory]
    [InlineData(JobOperationMode.Copy)]
    [InlineData(JobOperationMode.Move)]
    [InlineData(JobOperationMode.UpdateSync)]
    [InlineData(JobOperationMode.ContinuousSync)]
    public void Paste_PreservesDestinationAsOneArgument(JobOperationMode mode)
    {
        const string destination = @"C:\한글 공백 & #, 괄호(최종)\";
        var start = ShellExplorerCommand.CreatePasteStartInfo(@"C:\개발 앱\RoboCopy_GUI_Kr.exe", mode, destination);
        Assert.False(start.UseShellExecute);
        Assert.Empty(start.Arguments);
        Assert.Equal(3, start.ArgumentList.Count);
        var parsed = ShellStartupCommand.Parse(start.ArgumentList.ToArray());
        Assert.Equal(ShellStartupAction.PasteOperation, parsed.Action);
        Assert.Equal(destination, parsed.Path);
        Assert.Equal(mode, parsed.OperationMode);
    }

    [Fact]
    public void Factory_ExposesExplorerCommandAndRejectsAggregation()
    {
        var factory = new ShellSelectionClassFactory(_ => { }, modern: true);
        var id = typeof(IExplorerCommand).GUID;
        Assert.Equal(unchecked((int)0x80040110), factory.CreateInstance(new IntPtr(1), ref id, out var rejected));
        Assert.Equal(IntPtr.Zero, rejected);
        Assert.Equal(0, factory.CreateInstance(IntPtr.Zero, ref id, out var pointer));
        Assert.NotEqual(IntPtr.Zero, pointer);
        Marshal.Release(pointer);
    }

    [Fact]
    public void Subcommands_EnumerateCloneSkipAndResetWithDistinctNames()
    {
        var root = new ShellExplorerCommand(_ => { }, caption: null);
        Assert.Equal(0, root.GetFlags(out var flags));
        Assert.Equal(1u, flags);
        Assert.Equal(0, root.EnumSubCommands(out var enumerator));
        var first = new IExplorerCommand[1];
        Assert.Equal(0, enumerator!.Next(1, first, out var fetched));
        Assert.Equal(1u, fetched);
        Assert.Equal(0, first[0].GetState(null, false, out var state));
        Assert.Equal(0u, state); // Paste is available on a folder background.
        Assert.Equal(0, enumerator.Clone(out var clone));
        Assert.Equal(1, clone.Skip(10));
        Assert.Equal(1, clone.Next(1, first, out fetched));
        Assert.Equal(0u, fetched);
        enumerator.Reset();
        var all = new IExplorerCommand[5];
        Assert.Equal(1, enumerator.Next(5, all, out fetched));
        Assert.Equal(4u, fetched);
        var names = all.Take(4).Select(command => { command.GetCanonicalName(out var name); return name; }).ToArray();
        Assert.Equal(4, names.Distinct().Count());
    }

    [Fact]
    public void Copy_IsDirectAndHasNoSubmenu()
    {
        var copy = new ShellExplorerCommand(_ => { });
        Assert.Equal(0, copy.GetFlags(out var flags));
        Assert.Equal(0u, flags);
        Assert.Equal(unchecked((int)0x80004001), copy.EnumSubCommands(out var commands));
        Assert.Null(commands);
        copy.GetState(null, false, out var state);
        Assert.Equal(2u, state);
        Assert.Equal(unchecked((int)0x80070057), copy.Invoke(null, IntPtr.Zero));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealShellSelection_CopiesDirectlyAndRejectsPasteIntoFile(bool selectFolder)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "shell-test-" + Guid.NewGuid(), "한글 공백 & #, (폴더)");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "최종 #1 & (파일).txt");
        File.WriteAllText(path, "selection");
        IShellItem? item = null;
        IShellItemArray? selection = null;
        try
        {
            var id = typeof(IShellItem).GUID;
            var selectedPath = selectFolder ? folder : path;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(selectedPath, IntPtr.Zero, ref id, out item));
            id = typeof(IShellItemArray).GUID;
            Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromShellItem(item!, ref id, out selection));
            IReadOnlyList<string>? copied = null;
            var root = new ShellExplorerCommand(paths => copied = paths);
            Assert.Equal(0, root.Invoke(selection, IntPtr.Zero));
            Assert.Equal(new[] { selectedPath }, copied);
            var paste = new ShellExplorerCommand(_ => { }, caption: null);
            paste.EnumSubCommands(out var enumerator);
            var commands = new IExplorerCommand[4];
            enumerator!.Next(4, commands, out _);
            Assert.Equal(0, commands[0].GetState(selection, false, out var state));
            Assert.Equal(selectFolder ? 0u : 2u, state);
            if (!selectFolder)
                Assert.Equal(unchecked((int)0x80070057), commands[0].Invoke(selection, IntPtr.Zero));
        }
        finally
        {
            if (selection is not null) Marshal.ReleaseComObject(selection);
            if (item is not null) Marshal.ReleaseComObject(item);
            Directory.Delete(Path.GetDirectoryName(folder)!, true);
        }
    }

    [Fact]
    public void Package_RegistersDirectCopyForSelectionAndPasteForBackground()
    {
        var manifest = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "ExplorerPackage", "AppxManifest.xml"));
        XNamespace com = "http://schemas.microsoft.com/appx/manifest/com/windows10";
        XNamespace desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10/5";
        var server = manifest.Descendants(com + "ExeServer").Single();
        Assert.Equal("--shell-com-server", (string?)server.Attribute("Arguments"));
        Assert.Equal(ShellExplorerCommand.ClassId, (string?)server.Element(com + "Class")!.Attribute("Id"));
        var types = manifest.Descendants(desktop + "ItemType").ToArray();
        Assert.Equal(new[] { "*", "Directory", @"Directory\Background" }, types.Select(type => (string?)type.Attribute("Type")));
        Assert.Equal(new[] { ShellExplorerCommand.ClassId, ShellExplorerCommand.ClassId, ShellExplorerCommand.PasteClassId },
            types.Select(type => (string?)type.Element(desktop + "Verb")!.Attribute("Clsid")));
        Assert.Equal(2, server.Elements(com + "Class").Count());
        foreach (var verb in manifest.Descendants(desktop + "Verb"))
            Assert.Matches("^[A-Za-z0-9]+$", (string)verb.Attribute("Id")!);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid id, out IShellItem item);
    [DllImport("shell32.dll")]
    private static extern int SHCreateShellItemArrayFromShellItem(IShellItem item, ref Guid id, out IShellItemArray array);
}
