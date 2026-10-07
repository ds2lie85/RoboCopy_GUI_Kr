using RoboCopy_GUI_Kr.Core.Sync;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class DestinationOnlyRetentionTests
{
    [Fact]
    public void Update_KeepsUncheckedNewItemsAndRemovesRetainedItemsReturnedForDeletion()
    {
        var keep = new OneWaySyncExtraItem("D:\\keep.txt", "keep.txt", false, 10);
        var delete = new OneWaySyncExtraItem("D:\\delete.txt", "delete.txt", false, 20);
        var previouslyRetained = new OneWaySyncExtraItem("D:\\old.txt", "old.txt", false, 30);

        var result = DestinationOnlyRetention.Update(
            ["old.txt"],
            [keep, delete],
            [delete, previouslyRetained],
            [previouslyRetained]);

        Assert.Equal(["keep.txt"], result);
    }

    [Fact]
    public void Split_ExcludesRetainedItemsFromNewChangeNotification()
    {
        var retained = new OneWaySyncExtraItem("D:\\old.txt", "old.txt", false, 30);
        var added = new OneWaySyncExtraItem("D:\\new.txt", "new.txt", false, 40);

        var result = DestinationOnlyRetention.Split([retained, added], ["OLD.txt"]);

        Assert.Equal("new.txt", Assert.Single(result.NewItems).RelativePath);
        Assert.Equal("old.txt", Assert.Single(result.RetainedItems).RelativePath);
    }
}
