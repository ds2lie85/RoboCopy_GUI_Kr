namespace RoboCopy_GUI_Kr.Core.Errors;

public sealed record LockingProcessInfo(int ProcessId, string ApplicationName, string ServiceName, bool IsRestartable);
