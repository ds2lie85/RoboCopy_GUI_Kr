using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class NamedFileCopyServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"RoboCopy named copy {Guid.NewGuid():N}");

    [Fact]
    public async Task CopyAsync_CopiesKoreanPathToRequestedDifferentName()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "\uC6D0\uBCF8 \uC774\uB984.txt");
        var destination = Path.Combine(_root, "\uB300\uC0C1", "\uC6D0\uBCF8 \uC774\uB984 (1).txt");
        await File.WriteAllTextAsync(source, "\uD14C\uC2A4\uD2B8 \uB0B4\uC6A9");

        await new NamedFileCopyService().CopyAsync(source, destination, moveSource: false);

        Assert.True(File.Exists(source));
        Assert.Equal("\uD14C\uC2A4\uD2B8 \uB0B4\uC6A9", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task CopyAsync_MoveDeletesSourceOnlyAfterDestinationCompletes()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "\uC774\uB3D9 \uC6D0\uBCF8.bin");
        var destination = Path.Combine(_root, "\uB300\uC0C1", "\uC774\uB3D9 \uACB0\uACFC (1).bin");
        await File.WriteAllBytesAsync(source, Enumerable.Range(0, 4096).Select(index => (byte)index).ToArray());

        await new NamedFileCopyService().CopyAsync(source, destination, moveSource: true);

        Assert.False(File.Exists(source));
        Assert.Equal(4096, new FileInfo(destination).Length);
    }

    [Fact]
    public async Task CopyAsync_OverwriteReplacesOnlyTheChosenDestination()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "chosen.txt");
        var destination = Path.Combine(_root, "destination.txt");
        await File.WriteAllTextAsync(source, "chosen content");
        await File.WriteAllTextAsync(destination, "old content");

        await new NamedFileCopyService().CopyAsync(
            source,
            destination,
            moveSource: false,
            overwriteDestination: true);

        Assert.Equal("chosen content", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task CopyAsync_MoveReportsSourceDeletionFailureAfterDestinationCompletes()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "locked source.txt");
        var destination = Path.Combine(_root, "destination", "locked source (1).txt");
        await File.WriteAllTextAsync(source, "content copied before delete");

        await using var lockedSource = new FileStream(
            source,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var exception = await Assert.ThrowsAsync<MoveSourceDeletionException>(() =>
            new NamedFileCopyService().CopyAsync(source, destination, moveSource: true));

        Assert.Equal(source, exception.SourcePath);
        Assert.Equal(destination, exception.DestinationPath);
        Assert.Contains(exception.ErrorCode, new[] { 32, 33 });
        Assert.True(File.Exists(source));
        Assert.Equal("content copied before delete", await File.ReadAllTextAsync(destination));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
