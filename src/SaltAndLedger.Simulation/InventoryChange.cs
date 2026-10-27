namespace SaltAndLedger.Simulation
{
    public sealed class InventoryChange
    {
        public string GoodId { get; }
        public int Before { get; }
        public int After { get; }
        public int Delta => checked(After - Before);

        internal InventoryChange(string goodId, int before, int after)
        {
            GoodId = goodId;
            Before = before;
            After = after;
        }
    }

}
