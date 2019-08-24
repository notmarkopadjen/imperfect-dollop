using System;

namespace Paden.ImperfectDollop
{
    public class EntityEventArgs<T> : EventArgs
    {
        public T Entity { get; set; }
        public string OriginatorId { get; set; }
        public EntityAction EntityAction { get; set; }
    }
}
