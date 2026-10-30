namespace SpideyNet.Client.Audio;

internal interface IAudioOutput
{
    void Play(byte[] wave);
    void Stop();
}
