using System;

namespace Game.Rounds
{
    /// <summary>Run-only structures. Authorization/proximity belongs to the caller.</summary>
    public sealed class TycoonUnlocks
    {
        readonly bool[] _built;
        public TycoonUnlocks(int count) { _built = new bool[count]; }
        public bool IsBuilt(int index) => index >= 0 && index < _built.Length && _built[index];
        public bool TryBuild(int index, int cost, Func<int, bool> spend)
        {
            if (index < 0 || index >= _built.Length || _built[index] || cost <= 0 || spend == null)
                return false;
            if (!spend(cost)) return false;
            _built[index] = true;
            return true;
        }
        public void ResetRun() => Array.Clear(_built, 0, _built.Length);
    }
}
