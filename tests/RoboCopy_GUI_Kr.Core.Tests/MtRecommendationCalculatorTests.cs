using RoboCopy_GUI_Kr.Core.Performance;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class MtRecommendationCalculatorTests
{
    private readonly MtRecommendationCalculator _calculator = new();

    [Fact]
    public void SameVolume_UsesConservativeThreadCount()
    {
        var result = _calculator.Calculate(StorageKind.Fixed, StorageKind.Fixed, true, 32);

        Assert.Equal(4, result.ThreadCount);
        Assert.Equal(MtRecommendationReason.SameVolume, result.Reason);
    }

    [Theory]
    [InlineData(StorageKind.Network, StorageKind.Fixed, MtRecommendationReason.Network)]
    [InlineData(StorageKind.Fixed, StorageKind.Network, MtRecommendationReason.Network)]
    [InlineData(StorageKind.Removable, StorageKind.Fixed, MtRecommendationReason.Removable)]
    public void ConstrainedStorage_UsesBalancedThreadCount(
        StorageKind source,
        StorageKind destination,
        MtRecommendationReason reason)
    {
        var result = _calculator.Calculate(source, destination, false, 32);

        Assert.Equal(8, result.ThreadCount);
        Assert.Equal(reason, result.Reason);
    }

    [Theory]
    [InlineData(8, 8)]
    [InlineData(12, 16)]
    [InlineData(32, 16)]
    public void SeparateFixedVolumes_UsesProcessorCountAsSecondarySignal(int processorCount, int expected)
    {
        var result = _calculator.Calculate(StorageKind.Fixed, StorageKind.Fixed, false, processorCount);

        Assert.Equal(expected, result.ThreadCount);
        Assert.Equal(MtRecommendationReason.SeparateFixedVolumes, result.Reason);
    }
}
