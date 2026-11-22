using System;
using Game.Terrain;

namespace Game.Rounds
{
    /// <summary>Run-local purchases. Never persisted; a round transition does not reset this state.</summary>
    public sealed class RunZoneUnlocks : IWorldZoneAccess
    {
        int _mask = 1;
        bool _purchasing;
        public int Revision { get; private set; }
        public int UnlockedMask => _mask;
        public int UnlockedCount
        {
            get
            {
                int count = 0;
                for (int id = 1; id <= 9; id++) if (IsUnlocked(id)) count++;
                return count;
            }
        }
        public event Action Changed;
        public bool IsUnlocked(int zoneId) => zoneId >= 1 && zoneId <= 9 &&
            (_mask & (1 << (zoneId - 1))) != 0;
        public int Cost(int zoneId) => WorldZoneLayout.Cost(zoneId);

        public bool CanUnlock(int zoneId)
        {
            if (zoneId < 2 || zoneId > 9 || IsUnlocked(zoneId)) return false;
            for (int id = 1; id <= 9; id++)
                if (IsUnlocked(id) && WorldZoneLayout.AreAdjacent(id, zoneId)) return true;
            return false;
        }

        public bool TryUnlock(int zoneId, Func<int, bool> spend)
        {
            if (_purchasing || spend == null || !CanUnlock(zoneId)) return false;
            _purchasing = true;
            try
            {
                if (!spend(Cost(zoneId))) return false;
                _mask |= 1 << (zoneId - 1);
                Revision++;
                Changed?.Invoke();
                return true;
            }
            finally { _purchasing = false; }
        }

        public void ResetRun()
        {
            // SpendPoints can synchronously notify listeners. Re-entrant reset cannot
            // turn a successful charge into a purchase in another run.
            if (_purchasing) return;
            _mask = 1;
            Revision++;
            Changed?.Invoke();
        }
    }
}
