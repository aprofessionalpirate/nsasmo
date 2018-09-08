using System;

namespace MultiSpideyWinForms
{
    public class ConnectedPlayerInformation : Tuple<int, string>
    {
        public int Number { get { return Item1; } }
        public string Name { get { return Item2; } }

        public ConnectedPlayerInformation(int number, string name) : base(number, name)
        {
        }
    }
}
