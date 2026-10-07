using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace RoboCopy_GUI_Kr.Core.Robocopy;

public sealed record RobocopyRunResult(
    int ExitCode,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt)
{
    public TimeSpan Duration => FinishedAt - StartedAt;
}

public sealed class RobocopyRunner
{
    public async Task<RobocopyRunResult> RunAsync(
        RobocopyCommand command,
        Action<string>? outputReceived = null,
        Action<RobocopyProgress>? progressChanged = null,
        CancellationToken cancellationToken = default,
        Action<RobocopyFileProgress>? fileProgressChanged = null,
        bool listedFilesAreComplete = false)
    {
        ArgumentNullException.ThrowIfNull(command);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var consoleEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = consoleEncoding,
            StandardErrorEncoding = consoleEncoding,
        };

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var startedAt = DateTimeOffset.Now;

        if (!process.Start())
        {
            throw new InvalidOperationException("Robocopy process could not be started.");
        }

        using var cancellationRegistration = cancellationToken.Register(
            static state =>
            {
                var runningProcess = (Process)state!;
                try
                {
                    if (!runningProcess.HasExited)
                    {
                        runningProcess.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process exited between the HasExited check and Kill.
                }
            },
            process);

        var fileProgressTracker = command.Arguments.Count >= 2
            ? new RobocopyFileProgressTracker(command.Arguments[0], command.Arguments[1], listedFilesAreComplete)
            : null;
        var standardOutputTask = PumpAsync(
            process.StandardOutput,
            outputReceived,
            progressChanged,
            fileProgressChanged,
            fileProgressTracker);
        var standardErrorTask = PumpAsync(
            process.StandardError,
            outputReceived,
            progressChanged,
            fileProgressChanged,
            fileProgressTracker);

        await Task.WhenAll(process.WaitForExitAsync(), standardOutputTask, standardErrorTask).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return new RobocopyRunResult(process.ExitCode, startedAt, DateTimeOffset.Now);
    }

    private static async Task PumpAsync(
        StreamReader reader,
        Action<string>? outputReceived,
        Action<RobocopyProgress>? progressChanged,
        Action<RobocopyFileProgress>? fileProgressChanged,
        RobocopyFileProgressTracker? fileProgressTracker)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            outputReceived?.Invoke(line);
            if (RobocopyOutputParser.TryParseProgress(line, out var progress) && progress is not null)
            {
                progressChanged?.Invoke(progress);
            }

            if (fileProgressTracker?.TryProcessLine(line, out var fileProgress) == true
                && fileProgress is not null)
            {
                fileProgressChanged?.Invoke(fileProgress);
            }
        }
    }
}
