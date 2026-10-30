using System.Buffers.Binary;
using SpideyNet.Client.Audio;
using SpideyNet.Core;
using Xunit;

namespace SpideyNet.Client.Tests;

public sealed class TauntAudioTests
{
    [Fact]
    public void EveryBundledVoiceIsPlayablePcmAndReusesItsResource()
    {
        var output = new RecordingAudioOutput();
        var player = new TauntPlayer(output);
        foreach (var clip in Enum.GetValues<TauntId>())
        {
            player.Play(clip);
            var wave = Assert.IsType<byte[]>(output.Wave);
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wave, 0, 4));
            Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wave, 8, 4));
            Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(20)));
            Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(22)));
            Assert.Equal(22050, BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(24)));
            Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(34)));
            Assert.Equal(wave.Length - 44, BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(40)));
            Assert.InRange(wave.Length, 10000, 100000);
            Assert.Contains(wave.Skip(44), value => value != 0);
            player.Play(clip);
            Assert.Same(wave, output.Wave);
        }

        Assert.Throws<ArgumentException>(() => player.Play((TauntId)999));
        player.Stop();
        Assert.True(output.Stopped);
    }
}
