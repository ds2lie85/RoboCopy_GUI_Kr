param([string]$SelectionPath)
$ErrorActionPreference = 'Stop'
# Read-only activation probe: no copy, move, sync, or process termination.
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
[ComImport, Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDevelopmentExplorerCommand {
 [PreserveSig] int GetTitle(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] out string title);
 [PreserveSig] int GetIcon(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] out string icon);
 [PreserveSig] int GetToolTip(IntPtr items, out IntPtr tip);
 [PreserveSig] int GetCanonicalName(out Guid name);
 [PreserveSig] int GetState(IntPtr items, [MarshalAs(UnmanagedType.Bool)] bool slow, out uint state);
 [PreserveSig] int Invoke(IntPtr items, IntPtr context);
 [PreserveSig] int GetFlags(out uint flags);
 [PreserveSig] int EnumSubCommands(out IDevelopmentExplorerCommands commands);
}
[ComImport, Guid("A88826F8-186F-4987-AADE-EA0CEF8FBFE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDevelopmentExplorerCommands {
 [PreserveSig] int Next(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=0)] IDevelopmentExplorerCommand[] commands, out uint fetched);
 [PreserveSig] int Skip(uint count);
 [PreserveSig] int Reset();
 [PreserveSig] int Clone(out IDevelopmentExplorerCommands commands);
}
public static class DevelopmentExplorerProbe {
 [DllImport("ole32.dll")] public static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint context, ref Guid iid, out IntPtr command);
 [DllImport("shell32.dll", CharSet=CharSet.Unicode)] static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, out IntPtr item);
 [DllImport("shell32.dll")] static extern int SHCreateShellItemArrayFromShellItem(IntPtr item, ref Guid iid, out IntPtr items);
 public static string[] Verify(string selectionPath) {
  Guid clsid = new Guid("D8EAC1AC-8469-42AB-B78A-DB861D0355A9"), iid = typeof(IDevelopmentExplorerCommand).GUID;
  IntPtr pointer, pastePointer = IntPtr.Zero;
  Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid, IntPtr.Zero, 4, ref iid, out pointer));
  IDevelopmentExplorerCommand command = null;
  IDevelopmentExplorerCommand pasteCommand = null;
  IDevelopmentExplorerCommands enumerator = null;
  var children = new IDevelopmentExplorerCommand[4];
  IntPtr item = IntPtr.Zero, selection = IntPtr.Zero;
  try {
   command = (IDevelopmentExplorerCommand)Marshal.GetObjectForIUnknown(pointer);
   string title; Marshal.ThrowExceptionForHR(command.GetTitle(IntPtr.Zero, out title));
   uint flags; Marshal.ThrowExceptionForHR(command.GetFlags(out flags));
   if (flags != 0) throw new Exception("Copy must be directly clickable.");
   if (command.EnumSubCommands(out enumerator) != unchecked((int)0x80004001) || enumerator != null) throw new Exception("Copy must not have subcommands.");
   clsid = new Guid("CCB71B54-75C4-4BE7-AD86-E56A9B373F25");
   Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid, IntPtr.Zero, 4, ref iid, out pastePointer));
   pasteCommand = (IDevelopmentExplorerCommand)Marshal.GetObjectForIUnknown(pastePointer);
   Marshal.ThrowExceptionForHR(pasteCommand.GetFlags(out flags));
   if (flags != 1) throw new Exception("Paste subcommand flag missing.");
   Marshal.ThrowExceptionForHR(pasteCommand.EnumSubCommands(out enumerator));
   uint fetched; Marshal.ThrowExceptionForHR(enumerator.Next(4, children, out fetched));
   if (fetched != 4) throw new Exception("Expected four paste commands.");
   if (!String.IsNullOrEmpty(selectionPath)) {
    var itemId = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(selectionPath, IntPtr.Zero, ref itemId, out item));
    var arrayId = new Guid("B63EA76D-1F85-456F-A19C-48159EFA858B");
    Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromShellItem(item, ref arrayId, out selection));
    uint copyState; Marshal.ThrowExceptionForHR(command.GetState(selection, false, out copyState));
    if(copyState != 0) throw new Exception("Copy selection must be enabled.");
    for(int i=0; i<4; i++) {
     uint state; Marshal.ThrowExceptionForHR(children[i].GetState(selection, false, out state));
     uint expected = Directory.Exists(selectionPath) ? 0u : 2u;
     if(state != expected) throw new Exception("Unexpected remote selection state: " + i + ": " + state);
    }
   }
   var titles = new string[6]; titles[0] = title;
   Marshal.ThrowExceptionForHR(pasteCommand.GetTitle(IntPtr.Zero, out titles[1]));
   for(int i=0; i<4; i++) Marshal.ThrowExceptionForHR(children[i].GetTitle(IntPtr.Zero, out titles[i+2]));
   return titles;
  } finally {
   foreach(var child in children) if(child != null) Marshal.ReleaseComObject(child);
   if(enumerator != null) Marshal.ReleaseComObject(enumerator);
   if(command != null) Marshal.ReleaseComObject(command);
   if(pasteCommand != null) Marshal.ReleaseComObject(pasteCommand);
   if(pastePointer != IntPtr.Zero) Marshal.Release(pastePointer);
   if(selection != IntPtr.Zero) Marshal.Release(selection);
   if(item != IntPtr.Zero) Marshal.Release(item);
   Marshal.Release(pointer);
  }
 }
}
'@
Get-AppxPackage -Name 'RoboCopyGUIKr.Development.Explorer' | Select-Object Name, PackageFullName, InstallLocation
[DevelopmentExplorerProbe]::Verify($SelectionPath)
