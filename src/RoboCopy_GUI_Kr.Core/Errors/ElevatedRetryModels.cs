namespace RoboCopy_GUI_Kr.Core.Errors;

public sealed record ElevatedRetryFile(string SourcePath, string DestinationPath)
{
    public bool MoveAfterCopy { get; init; }

    public string? MoveSourceRoot { get; init; }

    public string? MoveDestinationRoot { get; init; }
}

public sealed record ElevatedRetryRequest
{
    public required IReadOnlyList<ElevatedRetryFile> Files { get; init; }

    public required string ResultPath { get; init; }

    public int RetryCount { get; init; } = 3;

    public int RetryWaitSeconds { get; init; } = 5;

    public bool Restartable { get; init; } = true;
}

public sealed record ElevatedRetryFileResult(
    string SourcePath,
    string DestinationPath,
    int ExitCode,
    bool IsSuccess,
    string? ErrorMessage);

public sealed record ElevatedRetryResult(IReadOnlyList<ElevatedRetryFileResult> Files);
