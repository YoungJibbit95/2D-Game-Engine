using System.Runtime.InteropServices;

namespace Game.Client.Rendering.Performance;

/// <summary>
/// Registers the game loop with Windows MMCSS so short high-refresh deadlines
/// are not delayed behind ordinary desktop work. Other platforms use a no-op.
/// </summary>
public sealed class GameThreadSchedulingScope : IDisposable
{
    private IntPtr _taskHandle;

    public GameThreadSchedulingScope()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            _taskHandle = NativeMethods.AvSetMmThreadCharacteristics("Games", out _);
            if (_taskHandle != IntPtr.Zero)
            {
                _ = NativeMethods.AvSetMmThreadPriority(_taskHandle, AvrtPriority.High);
            }
        }
        catch (DllNotFoundException)
        {
            _taskHandle = IntPtr.Zero;
        }
        catch (EntryPointNotFoundException)
        {
            _taskHandle = IntPtr.Zero;
        }
    }

    public bool IsActive => _taskHandle != IntPtr.Zero;

    public void Dispose()
    {
        if (_taskHandle == IntPtr.Zero)
        {
            return;
        }

        _ = NativeMethods.AvRevertMmThreadCharacteristics(_taskHandle);
        _taskHandle = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }

    private enum AvrtPriority
    {
        Low = -1,
        Normal = 0,
        High = 1,
        Critical = 2
    }

    private static class NativeMethods
    {
        [DllImport("avrt.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr AvSetMmThreadCharacteristics(string taskName, out uint taskIndex);

        [DllImport("avrt.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AvSetMmThreadPriority(IntPtr taskHandle, AvrtPriority priority);

        [DllImport("avrt.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AvRevertMmThreadCharacteristics(IntPtr taskHandle);
    }
}
