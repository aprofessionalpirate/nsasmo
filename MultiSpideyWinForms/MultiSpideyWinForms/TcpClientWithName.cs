using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MultiSpideyWinForms
{
    public class TcpClientWithName
    {
        public TcpClient Client { get; set; }
        public StreamWriter Writer { get; set; }
        public StreamReader Reader { get; set; }
        public string Name { get; set; }
        public int PlayerNumber { get; set; }
    }
}
