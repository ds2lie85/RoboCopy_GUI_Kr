using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RoboCopy_GUI_Kr.Core.Performance;

public sealed record SystemPerformanceProfile(
    string CpuName,
    int LogicalProcessorCount,
    ulong TotalPhysicalMemoryBytes,
    StorageKind SourceStorageKind,
    StorageKind DestinationStorageKind,
    string SourceVolume,
    string DestinationVolume,
    bool SameVolume);

public sealed class SystemPerformanceProfileDetector
{
    private readonly StorageDetector _storageDetector = new();

    public SystemPerformanceProfile Detect(string sourcePath, string destinationPath)
    {
        return new SystemPerformanceProfile(
            GetCpuName(),
            Environment.ProcessorCount,
            GetTotalPhysicalMemoryBytes(),
            _storageDetector.Detect(sourcePath),
            _storageDetector.Detect(destinationPath),
            _storageDetector.GetVolumeKey(sourcePath) ?? string.Empty,
            _storageDetector.GetVolumeKey(destinationPath) ?? string.Empty,
            _storageDetector.AreOnSameVolume(sourcePath, destinationPath));
    }

    private static string GetCpuName()
    {
        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? string.Empty;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            return string.Empty;
        }
    }

    private static ulong GetTotalPhysicalMemoryBytes()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 0;
        }

        var status = new MemoryStatusEx();
        return GlobalMemoryStatusEx(ref status)
            ? status.TotalPhysicalMemory
            : 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalMemory;
        public ulong AvailablePhysicalMemory;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;

        public MemoryStatusEx()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        }
    }
}
