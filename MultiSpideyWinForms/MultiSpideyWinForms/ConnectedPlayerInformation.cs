using System;

namespace MultiSpideyWinForms
{
    public class ConnectedPlayerInformation : Tuple<byte, string>
    {
        public byte Number { get { return Item1; } }
        public string Data { get { return Item2; } }

        public ConnectedPlayerInformation(byte number, string data) : base(number, data)
        {
        }
    }
}
