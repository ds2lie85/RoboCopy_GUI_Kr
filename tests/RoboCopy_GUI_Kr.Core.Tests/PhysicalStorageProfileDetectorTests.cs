using RoboCopy_GUI_Kr.Core.Performance;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class PhysicalStorageProfileDetectorTests
{
    [Fact]
    public void GetBusTypeName_MapsWindowsStorageBusValues()
    {
        Assert.Equal("NVMe", PhysicalStorageProfileDetector.GetBusTypeName(17));
        Assert.Equal("USB", PhysicalStorageProfileDetector.GetBusTypeName(7));
        Assert.Equal("SATA", PhysicalStorageProfileDetector.GetBusTypeName(11));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(100)]
    public void GetBusTypeName_ReturnsEmptyForUnknownValues(int busType)
    {
        Assert.Empty(PhysicalStorageProfileDetector.GetBusTypeName(busType));
    }
}
