using System;

namespace Game.Combat
{
    /// <summary>One active shot, never an inventory. Every replacement is a new purchase.</summary>
    public sealed class ShotLoadout
    {
        public ShotType Equipped { get; private set; } = ShotType.Basic;

        public bool Has(ShotType type) => ShotDefinitions.IsValid(type) && Equipped == type;

        // Kept as a rejecting compatibility API: there is no free equip path.
        public bool TryEquip(ShotType type) => false;

        public bool TryBuyAndEquip(ShotType type, Func<int, bool> spendPoints)
        {
            if (!ShotDefinitions.IsValid(type)) return false;
            if (spendPoints == null || !spendPoints(ShotDefinitions.Price(type))) return false;
            Equipped = type;
            return true;
        }

        public void ResetRun()
        {
            Equipped = ShotType.Basic;
        }
    }
}
