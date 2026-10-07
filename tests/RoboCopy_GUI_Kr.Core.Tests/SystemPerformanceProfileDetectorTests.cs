using RoboCopy_GUI_Kr.Core.Performance;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class SystemPerformanceProfileDetectorTests
{
    [Fact]
    public void Detect_ReportsProcessorMemoryAndVolumeRelationship()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".system-profile", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "원본 폴더");
        var destination = Path.Combine(testRoot, "대상 폴더");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);

        try
        {
            var profile = new SystemPerformanceProfileDetector().Detect(source, destination);

            Assert.True(profile.LogicalProcessorCount > 0);
            Assert.True(profile.SameVolume);
            Assert.Equal(profile.SourceVolume, profile.DestinationVolume, ignoreCase: true);
            if (OperatingSystem.IsWindows())
            {
                Assert.True(profile.TotalPhysicalMemoryBytes > 0);
            }
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
