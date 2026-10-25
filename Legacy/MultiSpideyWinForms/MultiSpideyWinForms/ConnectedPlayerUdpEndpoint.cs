using System;
using System.Net;

namespace MultiSpideyWinForms
{
    public class ConnectedPlayerUdpEndPoint : Tuple<byte, IPEndPoint>
    {
        public byte Number { get { return Item1; } }
        public IPEndPoint EndPoint { get { return Item2; } }

        public ConnectedPlayerUdpEndPoint(byte number, IPEndPoint endPoint) : base(number, endPoint)
        {
        }
    }
}
