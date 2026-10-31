# SpideyNet

<img width="1600" height="1000" alt="Bad Moon Falling" src="https://github.com/user-attachments/assets/804af919-229e-4ed3-a5d4-1d6f465ed221" />
<img width="1600" height="1000" alt="Head Lion" src="https://github.com/user-attachments/assets/13804b13-114e-4dc5-9149-d9fb62a57a9c" />
<img width="1600" height="1000" alt="Mine!" src="https://github.com/user-attachments/assets/c6f2a9a4-9344-4cf8-bc92-bbcc084e88a1" />

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
