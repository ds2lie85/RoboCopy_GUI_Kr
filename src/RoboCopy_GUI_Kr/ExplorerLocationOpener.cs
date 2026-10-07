using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using RoboCopy_GUI_Kr.Core.Paths;

namespace RoboCopy_GUI_Kr;

public static class ExplorerLocationOpener
{
    public static void Open(string requestedPath)
    {
        var target = ShellSelectionTargetResolver.Resolve(requestedPath);
        if (target.CanSelectItem && TryOpenAndSelect(target))
        {
            return;
        }

        OpenFolder(target.FolderPath);
    }

    private static bool TryOpenAndSelect(ShellSelectionTarget target)
    {
        IntPtr folderPidl = IntPtr.Zero;
        IntPtr itemPidl = IntPtr.Zero;
        try
        {
            if (SHParseDisplayName(target.FolderPath, IntPtr.Zero, out folderPidl, 0, out _) < 0
                || folderPidl == IntPtr.Zero)
            {
                return false;
            }

            if (SHParseDisplayName(target.ItemPath, IntPtr.Zero, out itemPidl, 0, out _) < 0
                || itemPidl == IntPtr.Zero)
            {
                return false;
            }

            var relativeItemPidl = ILFindLastID(itemPidl);
            if (relativeItemPidl == IntPtr.Zero)
            {
                return false;
            }

            return SHOpenFolderAndSelectItems(folderPidl, 1, [relativeItemPidl], 0) >= 0;
        }
        finally
        {
            if (itemPidl != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(itemPidl);
            }

            if (folderPidl != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(folderPidl);
            }
        }
    }

    private static void OpenFolder(string folderPath)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true,
        });

        if (process is null)
        {
            throw new Win32Exception();
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHParseDisplayName(
        string name,
        IntPtr bindingContext,
        out IntPtr itemIdList,
        uint attributesIn,
        out uint attributesOut);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern IntPtr ILFindLastID(IntPtr itemIdList);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHOpenFolderAndSelectItems(
        IntPtr folderItemIdList,
        uint itemCount,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] itemIdLists,
        uint flags);
}
