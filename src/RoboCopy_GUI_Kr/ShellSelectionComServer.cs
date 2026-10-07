using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace RoboCopy_GUI_Kr;

internal sealed class ShellSelectionComServer : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<IReadOnlyList<string>> _selectionReceived;
    private readonly ShellSelectionClassFactory _factory;
    private uint _registrationCookie;
    private DispatcherTimer? _timeoutTimer;

    public ShellSelectionComServer(Dispatcher dispatcher, Action<IReadOnlyList<string>> selectionReceived)
    {
        _dispatcher = dispatcher;
        _selectionReceived = selectionReceived;
        _factory = new ShellSelectionClassFactory(HandleSelection);
    }

    public void Start()
    {
        var classId = Guid.Parse(ExplorerContextMenuService.SelectionHandlerClassId);
        Marshal.ThrowExceptionForHR(CoRegisterClassObject(
            ref classId,
            _factory,
            ClassContext.LocalServer,
            RegistrationClass.MultipleUse,
            out _registrationCookie));

        _timeoutTimer = new DispatcherTimer(TimeSpan.FromMinutes(2), DispatcherPriority.Background, (_, _) =>
        {
            _timeoutTimer?.Stop();
            Application.Current.Shutdown();
        }, _dispatcher);
        _timeoutTimer.Start();
    }

    private void HandleSelection(IReadOnlyList<string> paths)
    {
        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                _selectionReceived(paths);
            }
            finally
            {
                Application.Current.Shutdown();
            }
        });
    }

    public void Dispose()
    {
        _timeoutTimer?.Stop();
        if (_registrationCookie != 0)
        {
            _ = CoRevokeClassObject(_registrationCookie);
            _registrationCookie = 0;
        }
    }

    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(
        ref Guid classId,
        [MarshalAs(UnmanagedType.Interface)] object classFactory,
        ClassContext context,
        RegistrationClass flags,
        out uint register);

    [DllImport("ole32.dll")]
    private static extern int CoRevokeClassObject(uint register);

    [Flags]
    private enum ClassContext : uint
    {
        LocalServer = 0x4,
    }

    private enum RegistrationClass : uint
    {
        MultipleUse = 1,
    }
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class ShellSelectionClassFactory : IClassFactory
{
    private readonly Action<IReadOnlyList<string>> _selectionReceived;

    public ShellSelectionClassFactory(Action<IReadOnlyList<string>> selectionReceived) =>
        _selectionReceived = selectionReceived;

    public int CreateInstance(IntPtr outer, ref Guid interfaceId, out IntPtr instance)
    {
        instance = IntPtr.Zero;
        if (outer != IntPtr.Zero)
        {
            return unchecked((int)0x80040110); // CLASS_E_NOAGGREGATION
        }

        var handler = new ShellSelectionExecuteCommand(_selectionReceived);
        var unknown = Marshal.GetIUnknownForObject(handler);
        try
        {
            return Marshal.QueryInterface(unknown, ref interfaceId, out instance);
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    public int LockServer(bool @lock) => 0;
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("9A59C322-8A2D-4D57-B10D-39971ED3DF78")]
internal sealed class ShellSelectionExecuteCommand : IExecuteCommand, IObjectWithSelection
{
    private readonly Action<IReadOnlyList<string>> _selectionReceived;
    private IReadOnlyList<string> _paths = [];

    public ShellSelectionExecuteCommand(Action<IReadOnlyList<string>> selectionReceived) =>
        _selectionReceived = selectionReceived;

    public int SetSelection(IShellItemArray selection)
    {
        var paths = new List<string>();
        var result = selection.GetCount(out var count);
        if (result < 0)
        {
            return result;
        }

        for (uint index = 0; index < count; index++)
        {
            if (selection.GetItemAt(index, out var item) < 0
                || item.GetDisplayName(ShellDisplayName.FileSystemPath, out var displayName) < 0)
            {
                continue;
            }

            try
            {
                var path = Marshal.PtrToStringUni(displayName);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    paths.Add(path);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(displayName);
            }
        }

        _paths = paths;
        return 0;
    }

    public int GetSelection(ref Guid interfaceId, out IntPtr selection)
    {
        selection = IntPtr.Zero;
        return unchecked((int)0x80004001); // E_NOTIMPL
    }

    public int SetKeyState(uint keyState) => 0;
    public int SetParameters(string? parameters) => 0;
    public int SetPosition(NativePoint point) => 0;
    public int SetShowWindow(int showWindow) => 0;
    public int SetNoShowUI(bool noShowUI) => 0;
    public int SetDirectory(string? directory) => 0;

    public int Execute()
    {
        _selectionReceived(_paths);
        return 0;
    }
}

[ComImport]
[Guid("00000001-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(IntPtr outer, ref Guid interfaceId, out IntPtr instance);

    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool @lock);
}

[ComImport]
[Guid("7F9185B0-CB92-43C5-80A9-92277A4F7B54")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IExecuteCommand
{
    [PreserveSig] int SetKeyState(uint keyState);
    [PreserveSig] int SetParameters([MarshalAs(UnmanagedType.LPWStr)] string? parameters);
    [PreserveSig] int SetPosition(NativePoint point);
    [PreserveSig] int SetShowWindow(int showWindow);
    [PreserveSig] int SetNoShowUI([MarshalAs(UnmanagedType.Bool)] bool noShowUI);
    [PreserveSig] int SetDirectory([MarshalAs(UnmanagedType.LPWStr)] string? directory);
    [PreserveSig] int Execute();
}

[ComImport]
[Guid("1C9CD5BB-98E9-4491-A60F-31AACC72B83C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IObjectWithSelection
{
    [PreserveSig] int SetSelection(IShellItemArray selection);
    [PreserveSig] int GetSelection(ref Guid interfaceId, out IntPtr selection);
}

[ComImport]
[Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray
{
    [PreserveSig] int BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);
    [PreserveSig] int GetPropertyStore(int flags, ref Guid interfaceId, out IntPtr propertyStore);
    [PreserveSig] int GetPropertyDescriptionList(IntPtr propertyKey, ref Guid interfaceId, out IntPtr descriptionList);
    [PreserveSig] int GetAttributes(uint flags, uint mask, out uint attributes);
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetItemAt(uint index, out IShellItem item);
    [PreserveSig] int EnumItems(out IntPtr enumerator);
}

[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    [PreserveSig] int BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);
    [PreserveSig] int GetParent(out IShellItem parent);
    [PreserveSig] int GetDisplayName(ShellDisplayName displayName, out IntPtr name);
    [PreserveSig] int GetAttributes(uint mask, out uint attributes);
    [PreserveSig] int Compare(IShellItem other, uint hint, out int order);
}

internal enum ShellDisplayName : uint
{
    FileSystemPath = 0x80058000,
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct NativePoint
{
    public readonly int X;
    public readonly int Y;
}
