using System.Text.Json;
using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Tests;

internal static class PlayerEdgeChecks
{
    internal static void Run()
    {
        var frame = new GameFrame(2, 32, 250, 1, 18);
        Check(NetRules.Valid(frame) && frame.ScreenY == -6, "Horizontal crouch above the top edge remains valid");
        foreach (var y in new byte[] { 0, 127, 128, 175, 208 })
        {
            Check((frame with { Y = y }).ScreenY == y, "Lower screen coordinates stay positive");
        }
        Check(!NetRules.Valid(frame with { Y = 209 }) && !NetRules.Valid(frame with { Y = 223 }), "Reject unsupported vertical overflow");
        Check(NetRules.Valid(frame with { Y = 224 }) && (frame with { Y = 224 }).ScreenY == -32, "Bounded wrapped coordinate");
        Check(!JsonSerializer.Serialize(frame).Contains("ScreenY"), "Screen Y is derived without adding a second wire coordinate");

        // Interpolating -6 to +6 must cross zero, never jump through the bottom of the room.
        var track = new RemoteTrack();
        track.Add(frame, 1000);
        track.Add(frame with { Y = 6 }, 1100);
        var middle = track.Sample((long)Math.Round(1050 + NetRules.TickMs * 2));
        Check(middle is not null && middle.Y == 0, "Smooth top-edge interpolation");
        Check(track.Sample((long)Math.Round(1025 + NetRules.TickMs * 2))?.ScreenY == -3, "Wrapped interpolation retains its sign");

        var source = frame with { X = 0, Facing = 0, WebHeld = true, Web = new WebState(47, 7, 7, 1) };
        var target = frame with { X = 40, Y = 0 };
        Check(InteractionRules.CanPush(source, target), "Push range crosses the top edge");
        Check(WebRules.Hits(source, target), "Web segment hits a partially visible player");
        var pulled = WebRules.Pull(source, target, 0.1);
        Check(pulled.ScreenY is >= -6 and <= 0 && pulled.Web?.OriginY == pulled.ScreenY + 7, "Pull geometry uses screen coordinates");

        // A source pixel on row seven is visible on row one when Y=-6, with both mirror axes.
        var raster = TileRaster.For(VideoMode.Vga);
        var sprite = Enumerable.Repeat((byte)255, SpriteRaster.FrameBytes).ToArray();
        for (var plane = 0; plane < 4; plane++)
        {
            if ((2 & (8 >> plane)) == 0)
            {
                sprite[plane * 63 + 7 * 3] &= 0xFD;
            }
        }
        for (byte facing = 0; facing < 8; facing++)
        {
            var buffer = new byte[raster.BufferBytes];
            SpriteRaster.Paint(frame with { Facing = facing }, SuitColour.Cyan, sprite, buffer);
            var x = (facing & 2) != 0 ? 32 - 3 + 23 - 6 : 32 + 6;
            var y = (facing & 4) != 0 ? 7 : 1;
            var offset = y * raster.RowBytes + x / 8;
            Check(buffer[offset] == (128 >> (x & 7)) && buffer.Count(value => value != 0) == 1,
                "Top-edge clipping preserves the visible source pixel without wrapping writes");
        }
        Console.WriteLine("Player edges: wrapped crouch/turn coordinates, clipping, interpolation and interactions passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
