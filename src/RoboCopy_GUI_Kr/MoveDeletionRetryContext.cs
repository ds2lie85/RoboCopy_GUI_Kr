using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr;

public sealed record MoveDeletionRetryContext(
    RobocopyJobOptions RetryOptions,
    string SourceRoot,
    string DestinationRoot);
