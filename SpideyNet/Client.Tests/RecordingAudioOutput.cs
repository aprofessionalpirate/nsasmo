using SpideyNet.Client.Audio;

namespace SpideyNet.Client.Tests;

internal sealed class RecordingAudioOutput : IAudioOutput
{
    internal byte[]? Wave { get; private set; }
    internal bool Stopped { get; private set; }

    public void Play(byte[] wave) => Wave = wave;
    public void Stop() => Stopped = true;
}
