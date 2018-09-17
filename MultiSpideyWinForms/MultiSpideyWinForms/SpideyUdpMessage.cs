using System;
using System.Text;

namespace MultiSpideyWinForms
{
    public static class SpideyUdpMessage
    {
        public static ASCIIEncoding AsciiEncoding = new ASCIIEncoding();

        public const int MESSAGE_HEADER_SIZE = 1;
        public const int PLAYER_NUMBER_SIZE = 1;
        public const int SPIDEY_DATA_SIZE = MemoryScanner.SPIDEY_DATA_SIZE;
        public const int LOCATION_DATA_SIZE = MemoryScanner.LOCATION_DATA_SIZE;

        public const int SPIN_A_WEB_MESSAGE_SIZE = MESSAGE_HEADER_SIZE + PLAYER_NUMBER_SIZE;
        public const int SPIDERMAN_MESSAGE_SIZE = MESSAGE_HEADER_SIZE + PLAYER_NUMBER_SIZE + SPIDEY_DATA_SIZE + LOCATION_DATA_SIZE;

        // Message Types
        public const byte SPIN_A_WEB = 1;
        public const byte SPIDERMAN = 2;

        public static byte[] CreateSpinAWebMessage(byte playerNumber)
        {
            return new[] { SPIN_A_WEB, playerNumber };
        }

        public static byte[] CreateSpidermanMessage(byte playerNumber, byte[] spideyData, byte[] locationData)
        {
            var spidermanMessage = new byte[SPIDERMAN_MESSAGE_SIZE];
            spidermanMessage[0] = SPIDERMAN;
            spidermanMessage[1] = playerNumber;
            Buffer.BlockCopy(spideyData, 0, spidermanMessage, MESSAGE_HEADER_SIZE + PLAYER_NUMBER_SIZE, SPIDEY_DATA_SIZE);
            Buffer.BlockCopy(locationData, 0, spidermanMessage, MESSAGE_HEADER_SIZE + PLAYER_NUMBER_SIZE + SPIDEY_DATA_SIZE, LOCATION_DATA_SIZE);
            return spidermanMessage;
        }

        public static bool ParseSpinAWebMessage(byte[] message, out byte playerNumber)
        {
            if (message.Length != SPIN_A_WEB_MESSAGE_SIZE)
            {
                playerNumber = 0;
                return false;
            }
            playerNumber = message[1];
            return true;
        }

        public static bool ParseSpidermanMessage(byte[] message, out byte playerNumber, out byte[] spideyData, out byte[] locationData)
        {
            if (message.Length != SPIDERMAN_MESSAGE_SIZE)
            {
                playerNumber = 0;
                spideyData = null;
                locationData = null;
                return false;
            }
            playerNumber = message[1];
            spideyData = new byte[SPIDEY_DATA_SIZE];
            locationData = new byte[LOCATION_DATA_SIZE];
            Buffer.BlockCopy(message, MESSAGE_HEADER_SIZE + PLAYER_NUMBER_SIZE, spideyData, 0, SPIDEY_DATA_SIZE);
            Buffer.BlockCopy(message, MESSAGE_HEADER_SIZE + PLAYER_NUMBER_SIZE + SPIDEY_DATA_SIZE, locationData, 0, LOCATION_DATA_SIZE);
            return true;
        }
    }
}
