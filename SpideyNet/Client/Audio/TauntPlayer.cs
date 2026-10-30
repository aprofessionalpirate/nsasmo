using SpideyNet.Core;

namespace SpideyNet.Client.Audio;

internal sealed class TauntPlayer(IAudioOutput output)
{
    private readonly Dictionary<TauntId, byte[]> clips = new();
    private readonly object gate = new();

    internal void Play(TauntId clip)
    {
        if (!Enum.IsDefined(clip))
        {
            throw new ArgumentException("Choose a supported taunt.", nameof(clip));
        }

        lock (gate)
        {
            if (!clips.TryGetValue(clip, out var wave))
            {
                // Embedded PCM works in the single-file release without a separate sound folder.
                using var resource = typeof(TauntPlayer).Assembly.GetManifestResourceStream(
                    $"SpideyNet.Taunts.{clip}.wav") ?? throw new IOException("The taunt audio is missing.");
                using var buffer = new MemoryStream();
                resource.CopyTo(buffer);
                wave = buffer.ToArray();
                clips.Add(clip, wave);
            }

            output.Play(wave);
        }
    }

    internal void Stop() => output.Stop();
}
