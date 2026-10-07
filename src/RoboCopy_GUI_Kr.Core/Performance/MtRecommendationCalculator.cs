namespace RoboCopy_GUI_Kr.Core.Performance;

public sealed class MtRecommendationCalculator
{
    public MtRecommendation Calculate(
        StorageKind sourceKind,
        StorageKind destinationKind,
        bool sameVolume,
        int logicalProcessorCount)
    {
        var (threadCount, reason) = (sourceKind, destinationKind, sameVolume) switch
        {
            (_, _, true) => (4, MtRecommendationReason.SameVolume),
            (StorageKind.Network, _, _) or (_, StorageKind.Network, _) => (8, MtRecommendationReason.Network),
            (StorageKind.Removable, _, _) or (_, StorageKind.Removable, _) => (8, MtRecommendationReason.Removable),
            (StorageKind.Fixed, StorageKind.Fixed, _) =>
                (logicalProcessorCount >= 12 ? 16 : 8, MtRecommendationReason.SeparateFixedVolumes),
            _ => (8, MtRecommendationReason.Unknown),
        };

        return new MtRecommendation(threadCount, sourceKind, destinationKind, sameVolume, reason);
    }
}
