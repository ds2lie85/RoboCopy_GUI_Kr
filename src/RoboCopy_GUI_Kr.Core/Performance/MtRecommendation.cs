namespace RoboCopy_GUI_Kr.Core.Performance;

public sealed record MtRecommendation(
    int ThreadCount,
    StorageKind SourceKind,
    StorageKind DestinationKind,
    bool SameVolume,
    MtRecommendationReason Reason);
