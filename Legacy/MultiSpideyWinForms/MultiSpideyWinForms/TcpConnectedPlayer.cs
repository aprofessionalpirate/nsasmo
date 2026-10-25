using System.IO;
using System.Net.Sockets;

namespace MultiSpideyWinForms
{
    public class TcpConnectedPlayer
    {
        public TcpClient Client { get; set; }
        public StreamWriter Writer { get; set; }
        public StreamReader Reader { get; set; }
        public ConnectedPlayerInformation PlayerInformation { get; set; }
    }
}
