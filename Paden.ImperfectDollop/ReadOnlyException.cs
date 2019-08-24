using System;

namespace Paden.ImperfectDollop
{
    public class ReadOnlyException : Exception
    {
        public ReadOnlyException(string message) : base(message)
        {

        }
    }
}
