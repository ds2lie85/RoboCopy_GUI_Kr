using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace RoboCopy_GUI_Kr.Core.Errors;

[SupportedOSPlatform("windows")]
public sealed class RestartManagerService
{
    public const uint GracefulShutdownActionFlags = 0;

    private const int ErrorMoreData = 234;
    private const int MaxApplicationName = 255;
    private const int MaxServiceName = 63;

    public IReadOnlyList<LockingProcessInfo> GetLockingProcesses(IEnumerable<string> paths)
    {
        var files = NormalizePaths(paths);
        var selfUsesFile = files.Any(IsCurrentProcessUsingFile);
        List<LockingProcessInfo> processes;
        try
        {
            processes = GetRestartManagerProcesses(files).ToList();
        }
        catch (Win32Exception) when (selfUsesFile && files.All(IsCurrentProcessUsingFile))
        {
            // Even if the OS lookup is unavailable, self-use is independently proven.
            // For mixed paths propagate the error instead of hiding unknown locks.
            processes = [];
        }
        // Restart Manager may omit its caller. Include its executable, native modules,
        // and managed assemblies explicitly so a self-overwrite is diagnosable.
        if (selfUsesFile
            && processes.All(process => process.ProcessId != Environment.ProcessId))
        {
            using var current = Process.GetCurrentProcess();
            processes.Add(new LockingProcessInfo(Environment.ProcessId, current.ProcessName, string.Empty, false));
        }
        return processes;
    }

    public static bool IsCurrentProcessUsingFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var fullPath = Path.GetFullPath(path);
        bool Matches(string? candidate) => !string.IsNullOrWhiteSpace(candidate)
            && string.Equals(fullPath, Path.GetFullPath(candidate), StringComparison.OrdinalIgnoreCase);
        if (Matches(Environment.ProcessPath)) return true;
        if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => !assembly.IsDynamic && Matches(assembly.Location)))
            return true;
        using var current = Process.GetCurrentProcess();
        return current.Modules.Cast<ProcessModule>().Any(module => Matches(module.FileName));
    }

    private IReadOnlyList<LockingProcessInfo> GetRestartManagerProcesses(IEnumerable<string> paths)
    {
        var files = NormalizePaths(paths);
        if (files.Length == 0)
        {
            return [];
        }

        var sessionKey = new StringBuilder(Guid.NewGuid().ToString("N"), 33);
        ThrowIfFailed(RmStartSession(out var sessionHandle, 0, sessionKey));
        try
        {
            ThrowIfFailed(RmRegisterResources(sessionHandle, (uint)files.Length, files, 0, null, 0, null));
            uint needed = 0;
            uint count = 0;
            uint rebootReasons = 0;
            var result = RmGetList(sessionHandle, out needed, ref count, null, ref rebootReasons);
            if (result == 0)
            {
                return [];
            }

            if (result != ErrorMoreData)
            {
                ThrowIfFailed(result);
            }

            var processes = new RestartManagerProcessInfo[needed];
            count = needed;
            ThrowIfFailed(RmGetList(sessionHandle, out needed, ref count, processes, ref rebootReasons));
            return processes
                .Take((int)count)
                .Select(process => new LockingProcessInfo(
                    process.Process.ProcessId,
                    process.ApplicationName,
                    process.ServiceShortName,
                    process.IsRestartable))
                .DistinctBy(process => process.ProcessId)
                .OrderBy(process => process.ApplicationName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        finally
        {
            _ = RmEndSession(sessionHandle);
        }
    }

    public void RequestGracefulShutdown(IEnumerable<string> paths)
    {
        var files = NormalizePaths(paths);
        if (files.Length == 0)
        {
            return;
        }

        var sessionKey = new StringBuilder(Guid.NewGuid().ToString("N"), 33);
        ThrowIfFailed(RmStartSession(out var sessionHandle, 0, sessionKey));
        try
        {
            ThrowIfFailed(RmRegisterResources(sessionHandle, (uint)files.Length, files, 0, null, 0, null));
            ThrowIfFailed(RmShutdown(sessionHandle, GracefulShutdownActionFlags, IntPtr.Zero));
        }
        finally
        {
            _ = RmEndSession(sessionHandle);
        }
    }

    public ForceTerminateResult ForceTerminateLockingProcesses(IEnumerable<string> paths)
    {
        var current = GetLockingProcesses(paths);
        var terminated = new List<int>();
        var failed = new List<int>();
        foreach (var lockingProcess in current)
        {
            if (ForceTerminationPolicy.IsProtected(lockingProcess.ProcessId, Environment.ProcessId))
            {
                failed.Add(lockingProcess.ProcessId);
                continue;
            }

            try
            {
                using var process = Process.GetProcessById(lockingProcess.ProcessId);
                process.Kill(entireProcessTree: false);
                if (process.WaitForExit(5000))
                {
                    terminated.Add(lockingProcess.ProcessId);
                }
                else
                {
                    failed.Add(lockingProcess.ProcessId);
                }
            }
            catch (ArgumentException)
            {
                terminated.Add(lockingProcess.ProcessId);
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                failed.Add(lockingProcess.ProcessId);
            }
        }

        var remaining = GetLockingProcesses(paths);
        return new ForceTerminateResult(terminated, failed, remaining);
    }

    private static string[] NormalizePaths(IEnumerable<string> paths) => paths
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static void ThrowIfFailed(int errorCode)
    {
        if (errorCode != 0)
        {
            throw new Win32Exception(errorCode);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UniqueProcess
    {
        public int ProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RestartManagerProcessInfo
    {
        public UniqueProcess Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxApplicationName + 1)]
        public string ApplicationName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxServiceName + 1)]
        public string ServiceShortName;

        public uint ApplicationType;
        public uint ApplicationStatus;
        public uint TerminalServicesSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool IsRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint sessionHandle, int sessionFlags, StringBuilder sessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint sessionHandle,
        uint fileCount,
        string[] fileNames,
        uint applicationCount,
        UniqueProcess[]? applications,
        uint serviceCount,
        string[]? serviceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(
        uint sessionHandle,
        out uint processInfoNeeded,
        ref uint processInfoCount,
        [In, Out] RestartManagerProcessInfo[]? affectedApplications,
        ref uint rebootReasons);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint sessionHandle);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmShutdown(uint sessionHandle, uint actionFlags, IntPtr statusCallback);
}

public static class ForceTerminationPolicy
{
    public static bool IsProtected(int processId, int currentProcessId) =>
        processId is <= 4 || processId == currentProcessId;
}

public sealed record ForceTerminateResult(
    IReadOnlyList<int> TerminatedProcessIds,
    IReadOnlyList<int> FailedProcessIds,
    IReadOnlyList<LockingProcessInfo> RemainingProcesses);
