using System;

namespace Paden.ImperfectDollop
{
    public class SourceConnectionStateChangedEventArgs : EventArgs
    {
        public bool IsAlive { get; set; }
    }
}
