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
        public const int LEVEL_DATA_SIZE = MemoryScanner.LEVEL_DATA_SIZE;

        public const int MESSAGE_HEADER_INDEX = MESSAGE_HEADER_SIZE - 1;
        public const int PLAYER_NUMBER_INDEX = MESSAGE_HEADER_SIZE + PLAYER_NUMBER_SIZE - 1;
        public const int SPIDEY_DATA_OFFSET = MESSAGE_HEADER_SIZE + PLAYER_NUMBER_SIZE;
        public const int LEVEL_DATA_INDEX = MESSAGE_HEADER_SIZE + PLAYER_NUMBER_SIZE + SPIDEY_DATA_SIZE + LEVEL_DATA_SIZE - 1;

        public const int SPIN_A_WEB_MESSAGE_SIZE = MESSAGE_HEADER_SIZE + PLAYER_NUMBER_SIZE;
        public const int SPIDERMAN_MESSAGE_SIZE = MESSAGE_HEADER_SIZE + PLAYER_NUMBER_SIZE + SPIDEY_DATA_SIZE + LEVEL_DATA_SIZE;

        // Message Types
        public const byte SPIN_A_WEB = 1;
        public const byte SPIDERMAN = 2;

        public static byte[] CreateSpinAWebMessage(byte playerNumber)
        {
            return new[] { SPIN_A_WEB, playerNumber };
        }

        public static byte[] CreateSpidermanMessage(byte playerNumber, byte[] spideyData, byte levelData)
        {
            var spidermanMessage = new byte[SPIDERMAN_MESSAGE_SIZE];
            spidermanMessage[MESSAGE_HEADER_INDEX] = SPIDERMAN;
            spidermanMessage[PLAYER_NUMBER_INDEX] = playerNumber;
            Buffer.BlockCopy(spideyData, 0, spidermanMessage, SPIDEY_DATA_OFFSET, SPIDEY_DATA_SIZE);
            spidermanMessage[LEVEL_DATA_INDEX] = levelData;
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

        public static bool ParseSpidermanMessage(byte[] message, out byte playerNumber, out byte[] spideyData, out byte levelData)
        {
            if (message.Length != SPIDERMAN_MESSAGE_SIZE)
            {
                playerNumber = 0;
                spideyData = null;
                levelData = 0;
                return false;
            }
            playerNumber = message[PLAYER_NUMBER_INDEX];
            spideyData = new byte[SPIDEY_DATA_SIZE];
            Buffer.BlockCopy(message, SPIDEY_DATA_OFFSET, spideyData, 0, SPIDEY_DATA_SIZE);
            levelData = message[LEVEL_DATA_INDEX];
            return true;
        }
    }
}
