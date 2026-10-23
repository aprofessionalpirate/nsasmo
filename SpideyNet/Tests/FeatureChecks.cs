using System.Buffers.Binary;
using System.Text.Json;
using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Tests;

internal static class FeatureChecks
{
    internal static void Run()
    {
        var actor = new byte[GameLayout.RecordSize];
        actor[GameLayout.CharacterOffset] = GameLayout.HiddenCharacter;
        actor[GameLayout.DescriptorOffset] = 0x4B;
        actor[GameLayout.DescriptorOffset + 1] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(actor.AsSpan(GameLayout.XOffset), 26000);
        BinaryPrimitives.WriteInt16LittleEndian(actor.AsSpan(GameLayout.PreviousXOffset), 25999);
        Check(SharedRules.Valid([EnemyRecord.Decode(actor, 1)]), "Dead off-screen actors remain valid domain state");
        var hidden = EnemyRecord.Hide(actor);
        Check(BinaryPrimitives.ReadInt16LittleEndian(hidden.AsSpan(GameLayout.XOffset)) == 0
            && BinaryPrimitives.ReadInt16LittleEndian(hidden.AsSpan(GameLayout.PreviousXOffset)) == 0
            && BinaryPrimitives.ReadInt16LittleEndian(hidden.AsSpan(GameLayout.BoundsLeftOffset)) == 0
            && hidden[GameLayout.BoundsTopOffset] == 0 && hidden[GameLayout.BoundsBottomOffset] == 0
            && hidden[GameLayout.YOffset] == 200 && hidden[GameLayout.HealthOffset] == 0
            && hidden[GameLayout.DescriptorOffset] == 0x4B, "Hide stale actor geometry without replacing native pointers");

        var source = new GameFrame(1, 11, 147, 0, 0x18, new WebState(58, 154, 18, 154), true);
        var target = source with { X = 51, Web = null, WebHeld = false };
        Check(WebRules.Hits(source, target), "A visible native web can hit another player");
        Check(!WebRules.Hits(source with { WebHeld = false }, target), "Releasing Space prevents a new attachment");
        Check(!WebRules.Hits(source, target with { Level = 2 }), "Web attachments cannot cross rooms");
        Check(!WebRules.Hits(source, target with { Y = 60 }), "Web attachments require an actual line hit");
        var pulled = WebRules.Pull(source, target, 1d / NetRules.TickRate);
        Check(pulled.X > source.X && pulled.X < target.X && pulled.Web?.EndX == target.X + 7,
            "Pull the shooter toward the target and keep the line attached");
        Check(WebRules.Pull(source, target, 100).X - source.X <= 10, "Bound pull distance after a delayed update");
        Check(NetRules.Valid(source) && JsonSerializer.Deserialize<GameFrame>(JsonSerializer.Serialize(source)) == source,
            "Native web geometry and held state survive the protocol");
        Check(!NetRules.Valid(source with { Web = new WebState(short.MaxValue, 0, 0, 0) }), "Reject unbounded web geometry");

        // A white web replaces yellow scenery across the clipped row; adjacent rows stay intact.
        {
            var raster = TileRaster.For(VideoMode.Vga);
            var buffer = new byte[raster.BufferBytes];
            foreach (var background in new byte[] { 0, 0xFF })
            {
                Array.Fill(buffer, background);
                raster.PaintWeb(new WebState(270, 10, -20, 10), buffer);
                for (var plane = 0; plane < raster.Planes; plane++)
                {
                    var row = plane * raster.PlaneBytes + 10 * raster.RowBytes;
                    var expected = plane < 2 ? (byte)0 : (byte)0xFF;
                    Check(buffer.AsSpan(row, raster.RowBytes).ToArray().All(value => value == expected),
                        "VGA web clips to the full visible row using native white palette index 3");
                    Check(buffer[row - 1] == background && buffer[row + raster.RowBytes] == background,
                        "VGA web leaves adjacent rows intact");
                }
            }

            // A single web pixel must also preserve every neighbouring pixel in its byte.
            Array.Fill(buffer, (byte)0xFF);
            raster.PaintWeb(new WebState(3, 10, 3, 10), buffer);
            var expectedBuffer = Enumerable.Repeat((byte)0xFF, raster.BufferBytes).ToArray();
            expectedBuffer[10 * raster.RowBytes] = 0xEF;
            expectedBuffer[raster.PlaneBytes + 10 * raster.RowBytes] = 0xEF;
            Check(buffer.SequenceEqual(expectedBuffer), "White web clears only its pixel's high-plane bits");
        }

        CheckInterpolation(source);
        Console.WriteLine("Feature checks: native web protocol/raster, attachment rules, inactive actors and enemy interpolation passed.");
    }

    private static void CheckInterpolation(GameFrame frame)
    {
        var puzzles = new byte[SharedRules.PuzzleBytes];
        var enemy = new EnemyState(1, 100, 50, 0, 192, 0x32);
        var scene = new GameScene(frame, puzzles, [enemy], []);
        var self = Guid.NewGuid();
        var room = new RoomState(1, Guid.NewGuid(), 1, [enemy], []);
        var world = new WorldState(NetRules.Version, self, 1, [], new SharedState(puzzles, room));
        var track = new SharedTrack();
        track.Capture(scene, world);
        track.Accept(scene, world, 1000);
        var next = world with { Tick = 2, Shared = new SharedState(puzzles, room with { Enemies = [enemy with { X = 120 }] }) };
        track.Accept(scene, next, 1033);
        var sample = track.Accept(scene, next, 1083)!;
        Check(sample.Room!.Enemies[0].X is >= 109 and <= 111, "Followers interpolate between network ticks at local render speed");
        var death = next with { Tick = 3, Shared = new SharedState(puzzles,
            room with { Enemies = [enemy with { Health = 0, Pose = GameLayout.HiddenCharacter }] }) };
        Check(track.Accept(scene, death, 1100)!.Room!.Enemies[0].Health == 0, "Interpolation does not delay enemy death");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
        {
            throw new InvalidOperationException(description);
        }
    }
}
