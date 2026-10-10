using SpideyNet.Core;
using SpideyNet.Game;

internal static class EnemyProbe
{
    private const int WaitMs = 1000;

    internal static async Task Run(int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var game = new GameBridge(processId, stop.Token);
        var scene = game.ReadScene() ?? throw new InvalidOperationException("Start the game.");
        var enemy = scene.Enemies.FirstOrDefault(value => value.Health > 0)
            ?? throw new InvalidOperationException("Choose a room with a living enemy.");
        var before = game.Inspect();
        game.SyncScene(new SharedState(SharedRules.Normalize(scene.Puzzles),
            new RoomState(scene.Frame.Level, Guid.Empty, 1, scene.Enemies, scene.Movers)), EnemyMode.Replica);
        await Task.Delay(WaitMs, stop.Token);
        var after = game.ReadScene() ?? throw new IOException("Game changed rooms.");
        var observed = after.Enemies.Single(value => value.Mover == enemy.Mover);
        Console.WriteLine($"Replica enemy: {enemy.X},{enemy.Y} -> {observed.X},{observed.Y}");
        if (enemy.X != observed.X || enemy.Y != observed.Y)
        {
            throw new InvalidOperationException("Follower enemy AI advanced without an owner update.");
        }
        if (game.Inspect().EnemyCount != before.EnemyCount)
        {
            throw new InvalidOperationException("Follower mode changed the native actor count.");
        }
        Console.WriteLine("Replica enemy stays still; native actor count unchanged.");
        game.ResumeEnemies();
        await Task.Delay(WaitMs, stop.Token);
        var resumed = game.ReadScene()!.Enemies.Single(value => value.Mover == enemy.Mover);
        if (resumed.X == observed.X && resumed.Y == observed.Y)
        {
            throw new InvalidOperationException("Native enemy AI did not resume.");
        }
        Console.WriteLine($"Native AI resumes: {resumed.X},{resumed.Y}.");
    }
}
