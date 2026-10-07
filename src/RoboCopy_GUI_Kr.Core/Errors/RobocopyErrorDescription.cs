using System.ComponentModel;

namespace RoboCopy_GUI_Kr.Core.Errors;

public static class RobocopyErrorDescription
{
    public static string GetSystemMessage(int errorCode) => new Win32Exception(errorCode).Message;
}
