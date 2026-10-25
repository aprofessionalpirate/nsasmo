using System;
using System.IO;
using System.Net;

namespace MultiSpideyWinForms
{
    public static class SpideyTcpMessage
    {
        // Message Types
        public const byte SPIDEY_SENSE = 1;
        public const byte TINGLING = 2;
        public const byte PLAYER_INFO = 3;
        public const byte UDP_INFO = 4;
        public const byte START = 5;

        public static void SendSpideySenseMessage(StreamWriter writer, string myName)
        {
            writer.WriteLine(SPIDEY_SENSE);
            writer.WriteLine(myName);
            writer.Flush();
        }

        public static void SendTinglingMessage(StreamWriter writer, string myName, byte yourPlayerNumber)
        {
            writer.WriteLine(TINGLING);
            writer.WriteLine(myName);
            writer.WriteLine(yourPlayerNumber);
            writer.Flush();
        }

        public static void SendPlayerInfoMessage(StreamWriter writer, byte otherPlayerNumber, string otherPlayerName)
        {
            writer.WriteLine(PLAYER_INFO);
            writer.WriteLine(otherPlayerNumber);
            writer.WriteLine(otherPlayerName);
            writer.Flush();
        }

        public static void SendUdpInfoMessage(StreamWriter writer, byte otherPlayerNumber, IPEndPoint otherPlayerUdpEndpoint)
        {
            writer.WriteLine(UDP_INFO);
            writer.WriteLine(otherPlayerNumber);
            writer.WriteLine(otherPlayerUdpEndpoint.Address.ToString());
            writer.WriteLine(otherPlayerUdpEndpoint.Port);
            writer.Flush();
        }

        public static void SendStartMessage(StreamWriter writer)
        {
            writer.WriteLine(START);
            writer.Flush();
        }

        public static void ParseSpideySenseMessage(StreamReader reader, out string playerName)
        {
            playerName = reader.ReadLine();
        }

        public static void ParseTinglingMessage(StreamReader reader, out string hostName, out byte myPlayerNumber)
        {
            hostName = reader.ReadLine();
            myPlayerNumber = Convert.ToByte(reader.ReadLine());
        }

        public static void ParsePlayerInfoMessage(StreamReader reader, out byte otherPlayerNumber, out string otherPlayerName)
        {
            otherPlayerNumber = Convert.ToByte(reader.ReadLine());
            otherPlayerName = reader.ReadLine();
        }

        public static void ParseUdpInfoMessage(StreamReader reader, out byte otherPlayerNumber, out IPEndPoint otherPlayerUdpEndpoint)
        {
            otherPlayerNumber = Convert.ToByte(reader.ReadLine());
            var otherPlayerIp = reader.ReadLine();
            var otherPlayerPort = Convert.ToInt32(reader.ReadLine());
            otherPlayerUdpEndpoint = new IPEndPoint(IPAddress.Parse(otherPlayerIp), otherPlayerPort);
        }
    }
}
