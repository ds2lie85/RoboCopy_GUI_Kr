using System.Xml.Linq;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class ResourceDictionaryTests
{
    private const string ForceTerminateWarning = "강제 종료는 예기치 못한 프로그램의 종료 및 컴퓨터 고장의 원인이 될 수 있습니다, 그래도 강행하시겠습니까?";
    [Theory]
    [InlineData("MainStrings.ko-KR.xaml")]
    [InlineData("AgentStrings.ko-KR.xaml")]
    public void StringResourceKeysAreUnique(string fileName)
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var duplicateKeys = document.Root!
            .Elements()
            .Select(element => element.Attribute(xaml + "Key")?.Value)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .GroupBy(key => key!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.Empty(duplicateKeys);
    }

    [Theory]
    [InlineData("MainStrings.ko-KR.xaml")]
    [InlineData("AgentStrings.ko-KR.xaml")]
    public void ForceTerminateWarningUsesRequiredExplicitWording(string fileName)
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var warning = document.Root!.Elements()
            .Single(element => element.Attribute(xaml + "Key")?.Value == "ForceTerminateWarning");

        Assert.Equal(ForceTerminateWarning, warning.Value);
    }
}
