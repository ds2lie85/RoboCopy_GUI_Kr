using RoboCopy_GUI_Kr.Core.Paths;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class FolderDialogStartPathTests
{
    [Fact]
    public void DestinationDialog_StartsAtSourceParent()
    {
        var source = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "work", "source");

        var result = FolderDialogStartPath.ResolveDestinationStartPath(source);

        Assert.Equal(Path.GetDirectoryName(source), result);
    }

    [Fact]
    public void EmptySource_HasNoPreferredStartDirectory()
    {
        Assert.Null(FolderDialogStartPath.ResolveDestinationStartPath(" "));
    }
}
