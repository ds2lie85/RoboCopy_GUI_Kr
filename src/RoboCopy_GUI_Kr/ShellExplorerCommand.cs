using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr;

// The command is hosted outside Explorer by the package's ExeServer.
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class ShellExplorerCommand : IExplorerCommand, IObjectWithSite
{
    internal const string ClassId = "D8EAC1AC-8469-42AB-B78A-DB861D0355A9";
    internal const string InstalledClassId = "47F48C24-34BA-445C-8B98-578FA482374E";
    internal const string PasteClassId = "CCB71B54-75C4-4BE7-AD86-E56A9B373F25";
    internal const string InstalledPasteClassId = "F29604CF-E758-4D75-9653-C42949056A6F";
    internal static string ActiveClassId => File.Exists(Path.Combine(AppContext.BaseDirectory,
        "ExplorerPackage", "AppxManifest.Installed.xml")) ? InstalledClassId : ClassId;
    internal static string ActivePasteClassId => ActiveClassId == InstalledClassId ? InstalledPasteClassId : PasteClassId;
    private readonly Action<IReadOnlyList<string>> _copy;
    private readonly string? _caption;
    private readonly JobOperationMode? _mode;
    private object? _site;

    internal ShellExplorerCommand(Action<IReadOnlyList<string>> copy, string? caption = "ExplorerCopyCaption", JobOperationMode? mode = null)
    {
        _copy = copy;
        _caption = caption;
        _mode = mode;
    }

    public int GetTitle(IShellItemArray? items, out string title)
    {
        title = Application.Current.Dispatcher.Invoke(() =>
            (string)Application.Current.FindResource(_caption ?? "ExplorerPasteMenuCaption"));
        return 0;
    }

    public int GetIcon(IShellItemArray? items, out string icon)
    {
        icon = Environment.ProcessPath + ",0";
        return 0;
    }

    public int GetToolTip(IShellItemArray? items, out IntPtr tooltip) { tooltip = IntPtr.Zero; return unchecked((int)0x80004001); }
    public int GetCanonicalName(out Guid name)
    {
        name = _caption is null ? Guid.Parse(ActivePasteClassId) : _mode is null
            ? Guid.Parse(ActiveClassId)
            : new Guid(0x41bef30b, 0x774b, 0x4738, 0xad, 0x8a, 0x40, 0xe5, 0x01, 0x6a, 0x8e, (byte)(0xb0 + (int)_mode.Value));
        return 0;
    }

    public int GetState(IShellItemArray? items, bool okToBeSlow, out uint state)
    {
        state = 0;
        if (_mode is null && _caption is not null)
        {
            state = items is not null && items.GetCount(out var count) >= 0 && count > 0 ? 0u : 2u;
            return 0;
        }
        // Do not read the saved JSON or scan the filesystem while constructing menus.
        if (items is not null && items.GetCount(out var selected) >= 0 && selected > 0)
        {
            if (selected != 1 || items.GetItemAt(0, out var item) < 0) { state = 2; return 0; }
            try
            {
                const uint folder = 0x20000000, filesystem = 0x40000000;
                state = item.GetAttributes(folder | filesystem, out var attributes) >= 0
                    && (attributes & (folder | filesystem)) == (folder | filesystem) ? 0u : 2u;
            }
            finally { Marshal.ReleaseComObject(item); }
        }
        return 0;
    }

    public int Invoke(IShellItemArray? items, IntPtr context)
    {
        try
        {
            if (_caption is null) return unchecked((int)0x80004001);
            if (_mode is null)
            {
                var paths = ReadPaths(items);
                if (paths.Count == 0) return unchecked((int)0x80070057);
                _copy(paths);
                return 0;
            }
            var selection = ReadPaths(items);
            // A non-folder selection must never fall back to the containing folder.
            var destination = selection.Count == 1 ? selection[0]
                : selection.Count == 0 ? ReadBackgroundFolder() : null;
            if (destination is null || !Directory.Exists(destination)) return unchecked((int)0x80070057);
            var start = CreatePasteStartInfo(Environment.ProcessPath!, _mode.Value, destination);
            using var process = Process.Start(start);
            return 0;
        }
        catch (Exception exception) { return Marshal.GetHRForException(exception); }
    }

    internal static ProcessStartInfo CreatePasteStartInfo(string executable, JobOperationMode mode, string destination)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        start.ArgumentList.Add("--shell-paste-operation");
        start.ArgumentList.Add(mode.ToString());
        start.ArgumentList.Add(destination);
        return start;
    }

    public int GetFlags(out uint flags) { flags = _caption is null ? 1u : 0u; return 0; }
    public int EnumSubCommands(out IEnumExplorerCommand? commands)
    {
        commands = null;
        if (_caption is not null) return unchecked((int)0x80004001);
        var children = new[]
        {
            new ShellExplorerCommand(_copy, "ExplorerPasteCaption", JobOperationMode.Copy),
            new ShellExplorerCommand(_copy, "ExplorerMoveCaption", JobOperationMode.Move),
            new ShellExplorerCommand(_copy, "ExplorerBackupCaption", JobOperationMode.UpdateSync),
            new ShellExplorerCommand(_copy, "ExplorerContinuousCaption", JobOperationMode.ContinuousSync),
        };
        foreach (var child in children) child.SetSite(_site);
        commands = new ShellExplorerCommandEnumerator(children);
        return 0;
    }

    public int SetSite(object? site) { _site = site; return 0; }
    public int GetSite(ref Guid id, out IntPtr site)
    {
        site = IntPtr.Zero;
        if (_site is null) return unchecked((int)0x80004002);
        var unknown = Marshal.GetIUnknownForObject(_site);
        try { return Marshal.QueryInterface(unknown, ref id, out site); }
        finally { Marshal.Release(unknown); }
    }

    private string? ReadBackgroundFolder()
    {
        if (_site is not IShellServiceProvider services) return null;
        var service = typeof(IFolderView).GUID;
        var viewId = service;
        if (services.QueryService(ref service, ref viewId, out var pointer) < 0) return null;
        object? view = null;
        object? folder = null;
        try
        {
            view = Marshal.GetObjectForIUnknown(pointer);
            var itemId = typeof(IShellItemArray).GUID;
            if (((IFolderView)view).GetFolder(ref itemId, out folder) < 0) return null;
            return ReadPaths((IShellItemArray)folder).SingleOrDefault();
        }
        finally
        {
            if (folder is not null) Marshal.ReleaseComObject(folder);
            if (view is not null) Marshal.ReleaseComObject(view);
            Marshal.Release(pointer);
        }
    }

    internal static IReadOnlyList<string> ReadPaths(IShellItemArray? items)
    {
        var paths = new List<string>();
        if (items is null || items.GetCount(out var count) < 0) return paths;
        for (uint i = 0; i < count; i++)
        {
            if (items.GetItemAt(i, out var item) < 0) continue;
            try { if (ReadPath(item) is { Length: > 0 } path) paths.Add(path); }
            finally { Marshal.ReleaseComObject(item); }
        }
        return paths;
    }

    private static string? ReadPath(IShellItem item)
    {
        if (item.GetDisplayName(ShellDisplayName.FileSystemPath, out var name) < 0) return null;
        try { return Marshal.PtrToStringUni(name); }
        finally { Marshal.FreeCoTaskMem(name); }
    }
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class ShellExplorerCommandEnumerator(IExplorerCommand[] commands, int position = 0) : IEnumExplorerCommand
{
    private int _position = position;
    public int Next(uint count, IExplorerCommand[] result, out uint fetched)
    {
        fetched = 0;
        while (fetched < count && _position < commands.Length) result[fetched++] = commands[_position++];
        return fetched == count ? 0 : 1;
    }
    public int Skip(uint count)
    {
        var remaining = commands.Length - _position;
        _position += (int)Math.Min(count, (uint)remaining);
        return count <= remaining ? 0 : 1;
    }
    public int Reset() { _position = 0; return 0; }
    public int Clone(out IEnumExplorerCommand clone) { clone = new ShellExplorerCommandEnumerator(commands, _position); return 0; }
}

[ComImport, Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IExplorerCommand
{
    [PreserveSig] int GetTitle(IShellItemArray? items, [MarshalAs(UnmanagedType.LPWStr)] out string title);
    [PreserveSig] int GetIcon(IShellItemArray? items, [MarshalAs(UnmanagedType.LPWStr)] out string icon);
    [PreserveSig] int GetToolTip(IShellItemArray? items, out IntPtr tooltip);
    [PreserveSig] int GetCanonicalName(out Guid name);
    [PreserveSig] int GetState(IShellItemArray? items, [MarshalAs(UnmanagedType.Bool)] bool okToBeSlow, out uint state);
    [PreserveSig] int Invoke(IShellItemArray? items, IntPtr context);
    [PreserveSig] int GetFlags(out uint flags);
    [PreserveSig] int EnumSubCommands(out IEnumExplorerCommand? commands);
}

[ComImport, Guid("A88826F8-186F-4987-AADE-EA0CEF8FBFE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumExplorerCommand
{
    [PreserveSig] int Next(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IExplorerCommand[] commands, out uint fetched);
    [PreserveSig] int Skip(uint count);
    [PreserveSig] int Reset();
    [PreserveSig] int Clone(out IEnumExplorerCommand clone);
}

[ComImport, Guid("FC4801A3-2BA9-11CF-A229-00AA003D7352"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IObjectWithSite
{
    [PreserveSig] int SetSite([MarshalAs(UnmanagedType.IUnknown)] object? site);
    [PreserveSig] int GetSite(ref Guid id, out IntPtr site);
}

[ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellServiceProvider
{
    [PreserveSig] int QueryService(ref Guid service, ref Guid id, out IntPtr result);
}

[ComImport, Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFolderView
{
    [PreserveSig] int GetCurrentViewMode(out uint mode);
    [PreserveSig] int SetCurrentViewMode(uint mode);
    [PreserveSig] int GetFolder(ref Guid id, [MarshalAs(UnmanagedType.IUnknown)] out object folder);
}
