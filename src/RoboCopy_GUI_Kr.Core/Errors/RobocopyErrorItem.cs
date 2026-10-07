namespace RoboCopy_GUI_Kr.Core.Errors;

public sealed record RobocopyErrorItem(
    int ErrorCode,
    RobocopyErrorCategory Category,
    string SourcePath,
    string DestinationPath,
    string Message,
    int Occurrences = 1);
