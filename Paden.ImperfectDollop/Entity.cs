namespace Paden.ImperfectDollop
{
    public abstract class Entity<T>
    {
        public T Id { get; set; }
        public ulong Version { get; set; }
    }
}
