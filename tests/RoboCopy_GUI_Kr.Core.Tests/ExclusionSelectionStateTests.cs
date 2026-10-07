using RoboCopy_GUI_Kr.Core.Exclusions;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ExclusionSelectionStateTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "RoboCopy_GUI_Kr_Source");

    [Fact]
    public void SetIncluded_UncheckingFileMakesAncestorsIndeterminate()
    {
        var state = new ExclusionSelectionState([], []);
        var folder = Path.Combine(Root, "Documents");
        var file = Path.Combine(folder, "제외.txt");

        state.SetIncluded(file, isDirectory: false, included: false);

        Assert.False(state.GetState(file, isDirectory: false));
        Assert.Null(state.GetState(folder, isDirectory: true));
        Assert.Null(state.GetState(Root, isDirectory: true));
    }

    [Fact]
    public void SetIncluded_UncheckingFolderUsesSingleParentExclusion()
    {
        var state = new ExclusionSelectionState([], []);
        var folder = Path.Combine(Root, "Temp");
        var nestedFile = Path.Combine(folder, "nested", "file.tmp");

        state.SetIncluded(nestedFile, isDirectory: false, included: false);
        state.SetIncluded(folder, isDirectory: true, included: false);

        Assert.Single(state.Directories);
        Assert.Empty(state.Files);
        Assert.False(state.GetState(nestedFile, isDirectory: false));
    }

    [Fact]
    public void SetIncluded_RecheckingFolderClearsDescendantExclusions()
    {
        var folder = Path.Combine(Root, "Temp");
        var file = Path.Combine(folder, "file.tmp");
        var state = new ExclusionSelectionState([folder], [file]);

        state.SetIncluded(folder, isDirectory: true, included: true);

        Assert.Empty(state.Directories);
        Assert.Empty(state.Files);
        Assert.True(state.GetState(folder, isDirectory: true));
        Assert.True(state.GetState(file, isDirectory: false));
    }

    [Fact]
    public void HasExcludedDirectoryAncestor_DistinguishesParentFromIndividualFileExclusion()
    {
        var folder = Path.Combine(Root, "HomeworkManager");
        var file = Path.Combine(folder, "homework.db");
        var state = new ExclusionSelectionState([], []);

        state.SetIncluded(file, isDirectory: false, included: false);
        Assert.False(state.HasExcludedDirectoryAncestor(file));

        state.SetIncluded(folder, isDirectory: true, included: false);
        Assert.True(state.HasExcludedDirectoryAncestor(file));
    }

    [Fact]
    public void FromIncludedChildrenUsesSameExclusionRulesAsTreeSelection()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"RoboCopyKr-Exclusions-{Guid.NewGuid():N}"));
        try
        {
            var included = Directory.CreateDirectory(Path.Combine(root.FullName, "선택 폴더")).FullName;
            var excluded = Directory.CreateDirectory(Path.Combine(root.FullName, "제외 폴더")).FullName;
            var excludedFile = Path.Combine(root.FullName, "제외 파일.txt");
            File.WriteAllText(excludedFile, "test");

            var state = ExclusionSelectionState.FromIncludedChildren(root.FullName, [included]);

            Assert.Equal([excluded], state.Directories);
            Assert.Equal([excludedFile], state.Files);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
