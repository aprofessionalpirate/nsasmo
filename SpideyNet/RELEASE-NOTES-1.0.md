# SpideyNet 1.0

The first release of SpideyNet brings multiplayer to the DOS game **The Amazing Spider-Man**. Each player runs their own game in DOSBox and connects through a shared relay.

## Highlights

- Up to **8 players** per private relay room.
- Shared player movement, animation and native web lines, with interpolation for smoother motion.
- Experimental enemy, puzzle, lift and helicopter synchronization.
- Experimental web attachment/pulling and a separate **V** shortcut to briefly freeze a nearby player. Players can pass through each other.
- Eight selectable Spidey colours, visible on your own character and other players.
- Player roster with room names, positions and health out of 100.
- Teleport to another player, or watch their DOSBox screen using **Spy**.
- .NET 10 Avalonia desktop client and a separate WebSocket relay with player connection/disconnection logs.

## Downloads and requirements

The attached **Client.exe** and **Relay.exe** are **Windows x64**, single-file, framework-dependent builds. They do not include the .NET runtime.

- **Players:** install the **.NET 10 Runtime (x64)** and download `Client.exe`.
- **Relay host:** install the **ASP.NET Core 10 Runtime (x64)** and download `Relay.exe`.
- If hosting and playing on the same PC, install both runtimes and download both executables.
- Bring your own game files and DOSBox. The game is not included. Verification used **DOSBox 0.74-3**, the **normal CPU core**, **3000 cycles** and **VGA**. VGA is the only supported video mode.

No SDK or source build is needed to use these release executables.

## Use the correct SPIDEY.EXE

SpideyNet currently supports the following executable:

**Working build — 231,936 bytes**

```text
SHA-256: 0695C840053C920BF198FA203AE64F8A028AA28EA186351AFA0AD4DE9EFD93F0
```

The alternate **40,511-byte** executable is **currently unsupported**:

```text
SHA-256: 14D09A7E8159445A405DA8CE3AB7D232A72EFE9849264646D83DCCA361AE2515
```

Check your actual executable in PowerShell:

```powershell
Get-FileHash -LiteralPath 'C:\Games\SPIDERMAN\SPIDEY.EXE' -Algorithm SHA256
```

## Start a relay

Open PowerShell in the folder containing `Relay.exe`. Only one person needs to host.

**Same-PC testing:**

```powershell
.\Relay.exe --urls http://127.0.0.1:5000
```

Use **`ws://127.0.0.1:5000`** in clients running on that PC.

**LAN play:** replace the example IP with the host PC's private LAN address:

```powershell
.\Relay.exe --urls http://192.168.178.45:5000
```

All players use **`ws://192.168.178.45:5000`**. Allow inbound TCP port **5000** on the host's Windows Firewall Private profile if needed. Keep the relay console open; press **Ctrl+C** to stop it.

Check connectivity from another PC:

```powershell
Invoke-RestMethod http://192.168.178.45:5000/health
```

The response should report protocol **8**. Running `Relay.exe` without endpoint configuration normally listens on `localhost:5000`, which other computers cannot reach.

Plain `ws://` is for trusted private networks and is unencrypted. Internet hosting requires a reachable TLS endpoint/reverse proxy with WebSocket forwarding and a **`wss://`** client URL. Changing `ws://` to `wss://` alone does not configure HTTPS.

## Join a game

1. Start DOSBox with `core=normal`, mount your game folder and run `SPIDEY.EXE`.
2. Select **1: VGA**, choose English, then **1: Play New Game**.
3. Launch `Client.exe`, choose **Refresh DOSBox**, and select your game process.
4. Enter your name and relay URL. Share one client's generated **room key** privately with the other players; everyone must use the same key and matching **Player interactions** and **Enemies / puzzles** settings.
5. Choose your Spidey colour and click **Connect**. Other players appear in-game when they enter the same native room.

Experimental rendering, player interactions and world sharing are enabled by default. **Space** fires the native web; **V** requests the separate player-freeze interaction. **Observe** rendering disables game-memory writes and the dependent interaction/shared-world options.

## Known limitations

Memory injection remains **experimental**, including in version 1.0. Not every room or long session has been fully tested; simultaneous human riders on moving scenery remain approximate. Native scenery moves in eight-pixel tile steps.

Use matching client/relay builds. Shared progress lives in relay memory: use a **new room key for a new game**; restarting the relay clears its rooms and progress. Disconnect normally before closing DOSBox. After an abnormal client exit, restart DOSBox to clear any remaining patches.

Spy streams screenshots and requires the watched DOSBox window to remain restored. Separate-Internet human play has not been comprehensively verified.

See the [wiki](https://github.com/aprofessionalpirate/nsasmo/wiki) for setup, troubleshooting, executable compatibility and technical notes.
