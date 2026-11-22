using System;
using Game.Core;
using Game.Rounds;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// One timer per temporary effect, with refresh rather than intensity stacking.
    /// Bind once to the player; NotifyDamaged must be called only after effective damage.
    /// The controller applies DamageReductionBonus before its existing total-reduction cap.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRunEffects : MonoBehaviour
    {
        readonly double[] _remaining = new double[DropDefinitions.EffectCount];
        readonly int[] _appliedFrame = new int[DropDefinitions.EffectCount];
        PlayerSphereController _player;
        Action<float> _heal;
        Func<bool> _runActive;
        Func<bool> _reserve;
        Func<bool> _recovery;
        double _undamagedSeconds;
        int _damagedFrame = -1;

        /// <summary>Apply, refresh, expiration and reset events. Remaining can be sampled for HUD timers.</summary>
        public event Action Changed;

        public float MovementMultiplier => Remaining(TemporaryEffect.Haste) > 0f ? 1.2f : 1f;
        public float DamageReductionBonus => Remaining(TemporaryEffect.Barrier) > 0f ? .1f : 0f;
        public float RecoveryDelayRemaining => (float)Math.Max(0d, DropDefinitions.RecoveryDelay - _undamagedSeconds);

        public void Bind(PlayerSphereController player)
        {
            _player = player;
            _heal = player != null ? new Action<float>(player.Heal) : null;
            _runActive = null;
            _reserve = null;
            _recovery = null;
        }

        /// <summary>
        /// Optional host/test seam without singletons or progression saves. GameFlow.Playing
        /// remains mandatory even when the run-active callback returns true.
        /// </summary>
        public void Configure(Func<bool> isRunActive, Action<float> heal,
            Func<bool> hasReserve = null, Func<bool> hasRecovery = null)
        {
            _player = null;
            _runActive = isRunActive;
            _heal = heal;
            _reserve = hasReserve;
            _recovery = hasRecovery;
        }

        bool CanTick()
        {
            if (!GameFlow.Playing) return false;
            if (_runActive != null) return _runActive();
            var round = RoundManager.Instance;
            return _player != null && _player.IsAlive && round != null && round.IsRunActive;
        }

        bool HasDrink(DrinkType type)
        {
            var round = RoundManager.Instance;
            return round != null && round.Drinks != null && round.Drinks.Has(type);
        }

        public float Remaining(TemporaryEffect type) =>
            DropDefinitions.IsValid(type) ? (float)_remaining[(int)type] : 0f;

        public int ModifyDamage(int damage)
        {
            if (damage <= 0 || Remaining(TemporaryEffect.Overcharge) <= 0f) return damage;
            int bonus = Mathf.Max(1, Mathf.RoundToInt(damage * .25f));
            return (int)Math.Min(int.MaxValue, (long)damage + bonus);
        }

        /// <summary>Reserve is sampled at collection, never applied retroactively to existing timers.</summary>
        public bool Apply(TemporaryEffect type)
        {
            if (!DropDefinitions.IsValid(type) || !CanTick()) return false;
            bool reserve = _reserve != null ? _reserve() : HasDrink(DrinkType.Reserve);
            _remaining[(int)type] = DropDefinitions.Duration(type) *
                (reserve ? DropDefinitions.ReserveDurationMultiplier : 1f);
            _appliedFrame[(int)type] = Time.frameCount;
            Changed?.Invoke();
            return true;
        }

        public void NotifyDamaged()
        {
            _undamagedSeconds = 0d;
            _damagedFrame = Time.frameCount;
        }

        public void ResetRun()
        {
            Array.Clear(_remaining, 0, _remaining.Length);
            _undamagedSeconds = 0d;
            Changed?.Invoke();
        }

        void Update() => Advance(Time.deltaTime, true);

        /// <summary>
        /// Active gameplay seconds only. Public for deterministic tests; normal hosts use Update.
        /// Integrating only the alive portion of the interval prevents a long frame over-healing.
        /// </summary>
        public void Tick(double gameplaySeconds)
        {
            Advance(gameplaySeconds, false);
        }

        void Advance(double gameplaySeconds, bool runtimeFrame)
        {
            if (!CanTick() || gameplaySeconds <= 0d ||
                double.IsNaN(gameplaySeconds) || double.IsInfinity(gameplaySeconds)) return;

            bool justCollectedRegen = runtimeFrame && _appliedFrame[(int)TemporaryEffect.Regeneration] == Time.frameCount;
            double healing = justCollectedRegen ? 0d :
                Math.Min(gameplaySeconds, _remaining[(int)TemporaryEffect.Regeneration]) * DropDefinitions.RegenerationPerSecond;
            double undamagedDelta = runtimeFrame && _damagedFrame == Time.frameCount ? 0d : gameplaySeconds;
            bool recovery = _recovery != null ? _recovery() : HasDrink(DrinkType.Recovery);
            if (recovery)
            {
                double waiting = Math.Max(0d, DropDefinitions.RecoveryDelay - _undamagedSeconds);
                double recoveredSeconds = undamagedDelta - waiting;
                if (recoveredSeconds > 1e-9d) healing += recoveredSeconds * DropDefinitions.RecoveryPerSecond;
            }
            _undamagedSeconds = Math.Min(DropDefinitions.RecoveryDelay, _undamagedSeconds + undamagedDelta);

            bool expired = false;
            for (int i = 0; i < _remaining.Length; i++)
            {
                if (_remaining[i] <= 0d) continue;
                if (runtimeFrame && _appliedFrame[i] == Time.frameCount) continue;
                _remaining[i] = Math.Max(0d, _remaining[i] - gameplaySeconds);
                // Avoid a residual sub-nanosecond timer from floating-point subtraction.
                if (_remaining[i] < 1e-9d) _remaining[i] = 0d;
                expired |= _remaining[i] == 0d;
            }

            if (healing > 0d) _heal?.Invoke((float)healing);
            if (expired) Changed?.Invoke();
        }
    }
}
