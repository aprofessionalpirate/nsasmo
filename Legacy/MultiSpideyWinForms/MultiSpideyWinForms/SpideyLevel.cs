namespace MultiSpideyWinForms
{
    public class SpideyLevel
    {
        public readonly byte Number;
        public readonly string Name;
        public readonly byte EnemyCount;

        public SpideyLevel(byte number, string name, byte enemyCount)
        {
            Number = number;
            Name = name;
            EnemyCount = enemyCount;
        }
    }
}
