using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using RoboCopy_GUI_Kr.Core.Errors;

namespace RoboCopy_GUI_Kr.Agent;

internal sealed record AgentElevatedWorkerResult(bool WasCanceled, ElevatedRetryResult? Result);

internal sealed class AgentElevatedWorkerClient
{
    public async Task<AgentElevatedWorkerResult> RunAsync(ElevatedRetryRequest request)
    {
        ElevatedRetryRequestValidator.Validate(request);
        var requestDirectory = Path.GetDirectoryName(request.ResultPath)
            ?? throw new ArgumentException("Result directory is required.", nameof(request));
        Directory.CreateDirectory(requestDirectory);
        var requestPath = Path.Combine(requestDirectory, "request.json");
        try
        {
            var requestBytes = JsonSerializer.SerializeToUtf8Bytes(request);
            await File.WriteAllBytesAsync(requestPath, requestBytes);
            var requestHash = Convert.ToHexString(SHA256.HashData(requestBytes));
            var workerPath = Path.Combine(AppContext.BaseDirectory, "RoboCopy_GUI_Kr.ElevatedWorker.exe");
            if (!File.Exists(workerPath))
            {
                throw new FileNotFoundException("Elevated worker was not found.", workerPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = workerPath,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory,
            };
            startInfo.ArgumentList.Add(requestPath);
            startInfo.ArgumentList.Add(requestHash);
            Process? process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
            {
                return new AgentElevatedWorkerResult(true, null);
            }

            if (process is null)
            {
                throw new InvalidOperationException("Elevated worker could not be started.");
            }

            using (process)
            {
                await process.WaitForExitAsync();
            }

            if (!File.Exists(request.ResultPath))
            {
                throw new InvalidDataException("Elevated worker did not create a result file.");
            }

            var result = JsonSerializer.Deserialize<ElevatedRetryResult>(await File.ReadAllTextAsync(request.ResultPath))
                ?? throw new InvalidDataException("Elevated worker returned an empty result.");
            return new AgentElevatedWorkerResult(false, result);
        }
        finally
        {
            try
            {
                if (Directory.Exists(requestDirectory))
                {
                    Directory.Delete(requestDirectory, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine(exception);
            }
        }
    }
}
