using System.Collections.Generic;
using System.Linq;

namespace MultiSpideyWinForms
{
    public static class SpideyLevels
    {
        private static bool _gotHighestEnemyCount = false;
        private static byte _highestEnemyCount = byte.MinValue;
        public static byte HighestEnemyCount
        {
            get
            {
                if (!_gotHighestEnemyCount)
                {
                    _highestEnemyCount = _mapSpideyLevels.Values.Max(s => s.EnemyCount);
                    // This is neccesary to avoid writing over spidey web data which is stored in C0 03 array
                    if (_highestEnemyCount < 5)
                    {
                        _highestEnemyCount = 5;
                    }
                    _gotHighestEnemyCount = true;
                }
                return _highestEnemyCount;
            }
        }

        private static readonly Dictionary<byte, SpideyLevel> _mapSpideyLevels = new Dictionary<byte, SpideyLevel>
        {
            { 0x00, new SpideyLevel(0x00,"                        ",0x01) },
            { 0x01, new SpideyLevel(0x01,"Midnight                ",0x02) },
            { 0x02, new SpideyLevel(0x02,"Foyer                   ",0x02) },
            { 0x03, new SpideyLevel(0x03,"Lift                    ",0x03) },
            { 0x04, new SpideyLevel(0x04,"Lift                    ",0x03) },
            { 0x05, new SpideyLevel(0x05,"Shaft                   ",0x01) },
            { 0x06, new SpideyLevel(0x06,"Black Thunder           ",0x01) },
            { 0x07, new SpideyLevel(0x07,"Take 1                  ",0x01) },
            { 0x08, new SpideyLevel(0x08,"Basement                ",0x01) },
            { 0x09, new SpideyLevel(0x09,"Storage Room            ",0x01) },
            { 0x0A, new SpideyLevel(0x0A,"Way Out                 ",0x03) },
            { 0x0B, new SpideyLevel(0x0B,"Wot! No Turtles?        ",0x03) },
            { 0x0C, new SpideyLevel(0x0C,"Into The Sewer          ",0x04) },
            { 0x0D, new SpideyLevel(0x0D,"The Great Drain Robbery ",0x01) },
            { 0x0E, new SpideyLevel(0x0E,"The Great Drain Robbery ",0x01) },
            { 0x0F, new SpideyLevel(0x0F,"TAKE 3                  ",0x01) },
            { 0x10, new SpideyLevel(0x10,"Bad Moon Rising         ",0x02) },
            { 0x11, new SpideyLevel(0x11,"Mummy's Revenge         ",0x02) },
            { 0x12, new SpideyLevel(0x12,"Very Grave Yard         ",0x01) },
            { 0x13, new SpideyLevel(0x13,"Rat Trap                ",0x02) },
            { 0x14, new SpideyLevel(0x14,"Under The Soil          ",0x02) },
            { 0x15, new SpideyLevel(0x15,"Bad Moon Falling        ",0x01) },
            { 0x16, new SpideyLevel(0x16,"Starry Starry Night     ",0x01) },
            { 0x17, new SpideyLevel(0x17,"TAKE 2                  ",0x01) },
            { 0x18, new SpideyLevel(0x18,"Fantasy Soundstage      ",0x02) },
            { 0x19, new SpideyLevel(0x19,"Cardboard City          ",0x02) },
            { 0x1A, new SpideyLevel(0x1A,"Paper Plates from Mars  ",0x02) },
            { 0x1B, new SpideyLevel(0x1B,"Space Ship              ",0x02) },
            { 0x1C, new SpideyLevel(0x1C,"The Lab                 ",0x02) },
            { 0x1D, new SpideyLevel(0x1D,"Time Machine            ",0x01) },
            { 0x1E, new SpideyLevel(0x1E,"Timeless Void           ",0x01) },
            { 0x1F, new SpideyLevel(0x1F,"TAKE 6                  ",0x01) },
            { 0x20, new SpideyLevel(0x20,"Air-locked              ",0x02) },
            { 0x21, new SpideyLevel(0x21,"Planet Fall             ",0x03) },
            { 0x22, new SpideyLevel(0x22,"Mission Control         ",0x02) },
            { 0x23, new SpideyLevel(0x23,"Weighting Room          ",0x01) },
            { 0x24, new SpideyLevel(0x24,"Weighting Room          ",0x01) },
            { 0x25, new SpideyLevel(0x25,"Weighting Room          ",0x01) },
            { 0x26, new SpideyLevel(0x26,"Weighting Room          ",0x01) },
            { 0x27, new SpideyLevel(0x27,"Weighting Room          ",0x01) },
            { 0x28, new SpideyLevel(0x28,"Lion Low                ",0x03) },
            { 0x29, new SpideyLevel(0x29,"Head Lion               ",0x01) },
            { 0x2A, new SpideyLevel(0x2A,"Leo's Maze              ",0x03) },
            { 0x2B, new SpideyLevel(0x2B,"Leo's Maze              ",0x03) },
            { 0x2C, new SpideyLevel(0x2C,"Leo's Maze              ",0x02) },
            { 0x2D, new SpideyLevel(0x2D,"Leo's Maze              ",0x03) },
            { 0x2E, new SpideyLevel(0x2E,"Roaring Flames          ",0x03) },
            { 0x2F, new SpideyLevel(0x2F,"TAKE 4                  ",0x01) },
            { 0x30, new SpideyLevel(0x30,"Cheers                  ",0x02) },
            { 0x31, new SpideyLevel(0x31,"Cactusville             ",0x03) },
            { 0x32, new SpideyLevel(0x32,"Vulture Gulch           ",0x02) },
            { 0x33, new SpideyLevel(0x33,"Mine!                   ",0x01) },
            { 0x34, new SpideyLevel(0x34,"Yours!                  ",0x01) },
            { 0x35, new SpideyLevel(0x35,"Behind Bars             ",0x03) },
            { 0x36, new SpideyLevel(0x36,"Behind Bars             ",0x03) },
            { 0x37, new SpideyLevel(0x37,"TAKE 5                  ",0x01) },
            { 0x38, new SpideyLevel(0x38,"All this and Jaws too   ",0x02) },
            { 0x39, new SpideyLevel(0x39,"Castle Entrance         ",0x02) },
            { 0x3A, new SpideyLevel(0x3A,"Puzzle Room             ",0x01) },
            { 0x3B, new SpideyLevel(0x3B,"The Pits                ",0x01) },
            { 0x3C, new SpideyLevel(0x3C,"Torture Chamber         ",0x04) },
            { 0x3D, new SpideyLevel(0x3D,"Torture Chamber         ",0x04) },
            { 0x3E, new SpideyLevel(0x3E,"Video Code              ",0x02) },
            { 0x3F, new SpideyLevel(0x3F,"Mysterio's End?         ",0x03) }
        };

        public static SpideyLevel GetSpideyLevel(byte levelNumber)
        {
            try
            {
                return _mapSpideyLevels[levelNumber];
            }
            catch (KeyNotFoundException)
            {
                return new SpideyLevel(0, "ERROR", 1);
            }
        }
    }
}
