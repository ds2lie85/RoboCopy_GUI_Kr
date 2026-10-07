using System.Runtime.InteropServices;

namespace RoboCopy_GUI_Kr.Agent;

internal static class MeteredNetworkDetector
{
    private const uint Fixed = 0x2;
    private const uint Variable = 0x4;
    private const uint OverDataLimit = 0x10000;
    private const uint Congested = 0x20000;
    private const uint Roaming = 0x40000;
    private const uint ApproachingDataLimit = 0x80000;

    public static bool IsMetered()
    {
        try
        {
            var manager = (INetworkCostManager)(object)new NetworkListManager();
            manager.GetCost(out var cost, IntPtr.Zero);
            return (cost & (Fixed | Variable | OverDataLimit | Congested | Roaming | ApproachingDataLimit)) != 0;
        }
        catch (COMException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    [ComImport]
    [Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B")]
    private sealed class NetworkListManager
    {
    }

    [ComImport]
    [Guid("DCB00008-570F-4A9B-8D69-199FDBA5723B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface INetworkCostManager
    {
        void GetCost(out uint cost, IntPtr destinationAddress);

        void GetDataPlanStatus(IntPtr dataPlanStatus, IntPtr destinationAddress);

        void SetDestinationAddresses(uint length, IntPtr destinationAddresses, [MarshalAs(UnmanagedType.VariantBool)] bool append);
    }
}
