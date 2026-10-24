# The Not So Amazing Spider-Man Online
[SpideyNet (.NET 10 rewrite)](SpideyNet/README.md)

This is an attempt to make the old DOS game 'The Amazing Spider-Man' capable of being played online.
http://www.mobygames.com/game/amazing-spider-man

For the current client and relay, build [SpideyNet](SpideyNet/README.md) with the .NET 10 SDK, or run `Build-Release.bat`. Select VGA in DOSBox and start a game before connecting.

The original Visual Studio 2017 project is preserved in `MultiSpideyWinForms`.

What it essentially does is read the X/Y position of Spider-Man and level title from the memory values of The Amazing Spider-Man and send these to the other connected players.
Right now it is extremely rough around the edges and the network code needs a lot of improvement.
