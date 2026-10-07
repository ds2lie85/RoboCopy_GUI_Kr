using System.Diagnostics;
using System.IO;
using RoboCopy_GUI_Kr.Core.ContinuousSync;

namespace RoboCopy_GUI_Kr;

public sealed class ContinuousSyncAgentLauncher
{
    private const string AgentExecutableName = "RoboCopy_GUI_Kr.Agent.exe";
    private readonly ContinuousSyncRunRequestRepository _runRequests = new();

    public void StartOrReload(IEnumerable<Guid>? configurationIds = null)
    {
        if (configurationIds is not null)
        {
            _runRequests.Enqueue(configurationIds);
        }

        if (TrySignalReload())
        {
            return;
        }

        var executablePath = Path.Combine(AppContext.BaseDirectory, AgentExecutableName);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("RoboCopy Agent executable was not found.", executablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException("RoboCopy Agent could not be started.");
    }

    public bool ReloadIfRunning() => TrySignalReload();

    private static bool TrySignalReload()
    {
        try
        {
            using var reloadEvent = EventWaitHandle.OpenExisting(ContinuousSyncAgentSignals.ReloadEventName);
            return reloadEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }
}
