namespace RoboCopy_GUI_Kr.Core.Models;

/// <summary>
/// Robocopy가 심볼릭 링크와 정션을 처리하는 방법입니다.
/// </summary>
public enum SymbolicLinkPolicy
{
    Exclude,
    CopyLinkItself,
    FollowTarget,
}
