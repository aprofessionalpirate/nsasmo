using System.Text.Json.Serialization;

namespace SpideyNet.Core;

public sealed record GameFrame(byte Level, short X, byte Y, byte Facing, byte Pose, WebState? Web = null, bool WebHeld = false, byte? Health = null, RoomLinks? Exits = null)
{
    // Full native health is 192; missing values identify peers that do not publish health yet.
    [JsonIgnore]
    public int? HealthPercent => Health is { } value
        ? Math.Min(100, (value * 100 + GameLayout.MaxHealth / 2) / GameLayout.MaxHealth)
        : null;

    // Native Y wraps near the top edge: 250 means -6. Do not cast every Y to sbyte,
    // because the lower half of the room (128..175) still uses positive coordinates.
    [JsonIgnore]
    public int ScreenY => Y >= 224 ? Y - 256 : Y;
}
