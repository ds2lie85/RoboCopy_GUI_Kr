using System.Security.Cryptography;
using System.Text.Json;
using RoboCopy_GUI_Kr.Core.Errors;
using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Moves;
using RoboCopy_GUI_Kr.Core.Robocopy;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length != 2 || !File.Exists(args[0]))
    {
        return 2;
    }

    var requestBytes = await File.ReadAllBytesAsync(args[0]);
    var expectedHash = Convert.FromHexString(args[1]);
    var actualHash = SHA256.HashData(requestBytes);
    if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
    {
        return 3;
    }

    var request = JsonSerializer.Deserialize<ElevatedRetryRequest>(requestBytes)
        ?? throw new InvalidDataException("The retry request is empty.");
    ElevatedRetryRequestValidator.Validate(request);

    var runner = new RobocopyRunner();
    var results = new List<ElevatedRetryFileResult>();
    foreach (var file in request.Files)
    {
        try
        {
            var sourceIsFile = File.Exists(file.SourcePath);
            var sourceIsDirectory = Directory.Exists(file.SourcePath);
            if (!sourceIsFile && !sourceIsDirectory)
            {
                throw new FileNotFoundException("Source file or directory was not found.", file.SourcePath);
            }

            var command = ElevatedRetryCommandFactory.Create(
                file,
                sourceIsDirectory,
                request.RetryCount,
                request.RetryWaitSeconds,
                request.Restartable);
            var run = await runner.RunAsync(command);
            var interpreted = RobocopyExitCodeInterpreter.Interpret(run.ExitCode);
            var isSuccess = interpreted.IsSuccess;
            string? errorMessage = interpreted.IsSuccess ? null : $"Robocopy exit code {run.ExitCode}.";
            if (isSuccess && file.MoveAfterCopy)
            {
                var moveOptions = new RobocopyJobOptions
                {
                    OperationMode = JobOperationMode.Move,
                    SourceDirectory = sourceIsDirectory ? file.SourcePath : Path.GetDirectoryName(file.SourcePath)!,
                    DestinationDirectory = sourceIsDirectory ? file.DestinationPath : Path.GetDirectoryName(file.DestinationPath)!,
                    FileSpecifications = sourceIsDirectory ? [] : [Path.GetFileName(file.SourcePath)],
                    RecurseSubdirectories = sourceIsDirectory,
                    IncludeEmptyDirectories = sourceIsDirectory,
                };
                var moveService = new VerifiedMoveService();
                var moveResult = await moveService.ExecuteAsync(moveOptions);
                if (moveResult.IsSuccess
                    && sourceIsFile
                    && file.MoveSourceRoot is not null
                    && file.MoveDestinationRoot is not null)
                {
                    moveService.DeleteVerifiedEmptyAncestors(
                        Path.GetDirectoryName(file.SourcePath)!,
                        file.MoveSourceRoot,
                        file.MoveDestinationRoot);
                }
                isSuccess = moveResult.IsSuccess;
                errorMessage = moveResult.IsSuccess
                    ? null
                    : $"Move verification failed for {moveResult.Failures.Count} item(s).";
            }

            results.Add(new ElevatedRetryFileResult(
                file.SourcePath,
                file.DestinationPath,
                run.ExitCode,
                isSuccess,
                errorMessage));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            results.Add(new ElevatedRetryFileResult(
                file.SourcePath,
                file.DestinationPath,
                -1,
                false,
                exception.Message));
        }
    }

    var resultDirectory = Path.GetDirectoryName(request.ResultPath)!;
    Directory.CreateDirectory(resultDirectory);
    await File.WriteAllTextAsync(request.ResultPath, JsonSerializer.Serialize(new ElevatedRetryResult(results)));
    return results.All(result => result.IsSuccess) ? 0 : 1;
}
