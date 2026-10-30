# SpideyNet

Multiplayer for the DOS game *The Amazing Spider-Man*, using a .NET 10 client
and relay. Each player runs the VGA game in DOSBox.

See [SpideyNet documentation](SpideyNet/README.md) for setup, features,
hosting and testing.

- [Run-Client.bat](Run-Client.bat): build and launch the client.
- [Run-Server-LAN.bat](Run-Server-LAN.bat): build and host a relay on your LAN.
- [Update-Server.bat](Update-Server.bat): rebuild and update an existing AWS relay deployment.
- [Build-Release.bat](Build-Release.bat): publish both executables to `outputs`.

The original WinForms multiplayer project, cheat tools and reverse-engineering
files are preserved in [Legacy](Legacy/README.md).
