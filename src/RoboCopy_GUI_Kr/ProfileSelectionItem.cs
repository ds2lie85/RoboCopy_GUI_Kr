using RoboCopy_GUI_Kr.Core.Profiles;

namespace RoboCopy_GUI_Kr;

public enum ProfileSelectionKind
{
    Default,
    Saved,
    New,
}

public sealed record ProfileSelectionItem(string Name, ProfileSelectionKind Kind, WorkProfile? Profile = null);

