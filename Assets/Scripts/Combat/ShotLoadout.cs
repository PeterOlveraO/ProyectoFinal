using System;

namespace Game.Combat
{
    /// <summary>Run-only ownership. The caller authorizes gameplay/proximity and spends run points.</summary>
    public sealed class ShotLoadout
    {
        int _owned = 1 << (int)ShotType.Basic;
        public ShotType Equipped { get; private set; } = ShotType.Basic;

        public bool Has(ShotType type) => ShotDefinitions.IsValid(type) && (_owned & (1 << (int)type)) != 0;

        public bool TryEquip(ShotType type)
        {
            if (!Has(type)) return false;
            Equipped = type;
            return true;
        }

        public bool TryBuyAndEquip(ShotType type, Func<int, bool> spendPoints)
        {
            if (!ShotDefinitions.IsValid(type)) return false;
            if (Has(type)) return TryEquip(type);
            if (spendPoints == null || !spendPoints(ShotDefinitions.Price(type))) return false;
            _owned |= 1 << (int)type;
            Equipped = type;
            return true;
        }

        public void ResetRun()
        {
            _owned = 1 << (int)ShotType.Basic;
            Equipped = ShotType.Basic;
        }
    }
}
