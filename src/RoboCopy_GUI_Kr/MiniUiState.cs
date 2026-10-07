namespace RoboCopy_GUI_Kr;

internal sealed record MiniUiState(
    string SourceName,
    string DestinationName,
    string OperationName,
    bool IsConfigured,
    int TotalFiles,
    int CompletedFiles,
    double Percent,
    bool IsIndeterminate,
    bool IsPaused,
    bool CanPauseOrResume,
    bool IsCompleted);
