using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SpideyNet.Client.Audio;

internal sealed class WaveAudioOutput : IAudioOutput
{
    private const uint AsyncMemoryNoDefault = 0x0001 | 0x0004 | 0x0002;
    private readonly object gate = new();
    private GCHandle current;

    public void Play(byte[] wave)
    {
        lock (gate)
        {
            // PlaySound retains the PCM pointer asynchronously. Stop before releasing the old pin.
            Stop();
            current = GCHandle.Alloc(wave, GCHandleType.Pinned);
            if (!PlaySound(current.AddrOfPinnedObject(), IntPtr.Zero, AsyncMemoryNoDefault))
            {
                var error = Marshal.GetLastWin32Error();
                current.Free();
                throw new Win32Exception(error, "Windows could not play the taunt.");
            }
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            if (current.IsAllocated)
            {
                PlaySound(IntPtr.Zero, IntPtr.Zero, 0);
                current.Free();
            }
        }
    }

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(IntPtr sound, IntPtr module, uint flags);
}
