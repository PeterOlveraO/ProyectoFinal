using System;
using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Terrain;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>
    /// Bind to RoundManager once. NotifyDeath receives the enemy's captured bonus point
    /// value, NOT a value recomputed from the current round. ResetRun at start/finish/abandon,
    /// never between Fighting/Clearing/Preparing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDropSystem : MonoBehaviour
    {
        readonly List<GroundPickup> _pickups = new List<GroundPickup>(DropDefinitions.PoolCapacity);
        RoundManager _round;
        PlayerSphereController _player;
        Transform _contactTarget;
        CharacterController _controller;
        PlayerRunEffects _effects;
        Transform _root;
        Camera _camera;
        DropPickupArt _art;
        Func<bool> _runActive;
        Func<int, bool> _collectPoints;
        Func<GroundPickup, bool> _collector;
        bool _customPlayer;
        int _recycleCursor;

        /// <summary>
        /// Optional world-service override. Receives the original death position, returns a
        /// valid ground anchor in the same accessible region. Both rewards share that anchor.
        /// The default is a conservative, short terrain-only path near the death.
        /// </summary>
        public TryDropPlacement TryPlacement { get; set; }
        public IReadOnlyList<GroundPickup> Pickups => _pickups;
        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (var pickup in _pickups)
                    if (pickup != null && pickup.IsAvailable) count++;
                return count;
            }
        }

        public void Bind(RoundManager round)
        {
            _round = round;
            _runActive = null;
            _customPlayer = false;
            _player = null;
            _contactTarget = null;
            _controller = null;
            _effects = null;
            _collectPoints = round != null ? new Func<int, bool>(round.CollectBonusPoints) : null;
            _collector = CollectPickup;
            CachePlayer();
        }

        /// <summary>Optional host/test seam; no RoundManager, player saves or input are needed.</summary>
        public void Configure(Transform player, PlayerRunEffects effects, Func<bool> isRunActive,
            Func<int, bool> collectBonusPoints, TryDropPlacement placement = null)
        {
            _round = null;
            _customPlayer = true;
            _contactTarget = player;
            _player = player != null ? player.GetComponent<PlayerSphereController>() : null;
            _controller = player != null ? player.GetComponent<CharacterController>() : null;
            _effects = effects;
            _runActive = isRunActive;
            _collectPoints = collectBonusPoints;
            _collector = CollectPickup;
            TryPlacement = placement;
        }

        bool CanTick() => GameFlow.Playing &&
            (_runActive != null ? _runActive() : _round != null && _round.IsRunActive);

        void CachePlayer()
        {
            if (_customPlayer) return;
            var current = PlayerSphereController.Instance;
            if (_player == current && _contactTarget != null) return;
            _player = current;
            _contactTarget = current != null ? current.transform : null;
            _controller = current != null ? current.GetComponent<CharacterController>() : null;
        }

        public void NotifyDeath(Vector3 position, bool boss, int bonusPoints) =>
            NotifyDeath(position, boss, bonusPoints, null);

        /// <summary>Injected [0,1] RNG is optional and only used for the independent loot rolls.</summary>
        public void NotifyDeath(Vector3 position, bool boss, int bonusPoints, Func<float> random01)
        {
            if (!CanTick() || !Finite(position)) return;
            DeathDropRoll roll = DropDefinitions.Roll(boss, random01);
            bool points = roll.HasPoints && bonusPoints > 0;
            if (!roll.HasTemporary && !points) return;
            Vector3 ground;
            bool placed = TryPlacement != null
                ? TryPlacement(position, out ground)
                : TryDefaultPlacement(position, out ground);
            if (!placed || !Finite(ground)) return;

            CachePlayer();
            if (_camera == null) _camera = Camera.main;
            if (roll.HasTemporary)
                Spawn(ground, GroundPickupKind.TemporaryEffect, roll.Effect, 0, points ? -.24f : 0f);
            if (points)
                Spawn(ground, GroundPickupKind.BonusPoints, default, bonusPoints, roll.HasTemporary ? .24f : 0f);
        }

        void Spawn(Vector3 ground, GroundPickupKind kind, TemporaryEffect effect, int points, float visualOffset)
        {
            if (!CanTick()) return; // A previous collection's event may have finished the run.
            GroundPickup pickup = Acquire();
            pickup.Activate(ground, kind, effect, points, _art.Get(kind, effect),
                kind == GroundPickupKind.BonusPoints ? DropPickupArt.PointsColor : DropDefinitions.Color(effect),
                visualOffset, _camera);
            // Standing on the death position counts immediately; no enter-trigger edge is required.
            if (CanCollect()) pickup.TryCollect(FeetPosition(), _collector);
        }

        GroundPickup Acquire()
        {
            for (int i = 0; i < _pickups.Count; i++)
            {
                if (_pickups[i] == null) return _pickups[i] = CreatePickup();
                if (!_pickups[i].IsAvailable) return _pickups[i];
            }
            if (_pickups.Count < DropDefinitions.PoolCapacity)
            {
                var pickup = CreatePickup();
                _pickups.Add(pickup);
                return pickup;
            }
            // A pathological burst cannot allocate indefinitely. Retire the oldest live entry.
            int oldestIndex = _recycleCursor;
            for (int i = 0; i < _pickups.Count; i++)
                if (_pickups[i].Age > _pickups[oldestIndex].Age) oldestIndex = i;
            GroundPickup oldest = _pickups[oldestIndex];
            _recycleCursor = (oldestIndex + 1) % _pickups.Count;
            oldest.Retire();
            return oldest;
        }

        GroundPickup CreatePickup()
        {
            if (_art == null) _art = new DropPickupArt();
            if (_root == null)
                _root = new GameObject("RunGroundPickups").transform; // Independent of enemies/terrain chunks.
            var go = new GameObject("GroundPickup");
            go.transform.SetParent(_root, false);
            var pickup = go.AddComponent<GroundPickup>();
            pickup.BuildVisual(_art.Ring);
            return pickup;
        }

        bool CanCollect() => CanTick() && _contactTarget != null &&
            _contactTarget.gameObject.activeInHierarchy && (_player == null || _player.IsAlive);

        Vector3 FeetPosition()
        {
            if (_controller == null) return _contactTarget.position;
            return _controller.transform.TransformPoint(_controller.center) -
                Vector3.up * (_controller.height * .5f * Mathf.Abs(_controller.transform.lossyScale.y));
        }

        bool CollectPickup(GroundPickup pickup)
        {
            if (!CanCollect()) return false;
            if (pickup.Kind == GroundPickupKind.BonusPoints)
                return _collectPoints != null && _collectPoints(pickup.BonusPoints);
            var effects = _customPlayer ? _effects : _player != null ? _player.RunEffects : null;
            return effects != null && effects.Apply(pickup.Effect);
        }

        void LateUpdate() => Advance(Time.deltaTime, true);

        /// <summary>Normal runtime uses LateUpdate, after player movement. Never uses visibility for aging.</summary>
        public void Tick(double gameplaySeconds) => Advance(gameplaySeconds, false);

        void Advance(double gameplaySeconds, bool runtimeFrame)
        {
            if (!CanTick() || gameplaySeconds <= 0d ||
                double.IsNaN(gameplaySeconds) || double.IsInfinity(gameplaySeconds)) return;
            CachePlayer();
            bool contact = CanCollect();
            Vector3 feet = contact ? FeetPosition() : default;
            for (int i = 0; i < _pickups.Count; i++)
            {
                var pickup = _pickups[i];
                if (pickup == null || !pickup.IsAvailable) continue;
                // The spawn frame's delta preceded the death. Start aging on the next frame.
                if (!runtimeFrame || pickup.SpawnFrame != Time.frameCount)
                    pickup.Tick(gameplaySeconds, _camera); // Expiration wins at exactly five seconds.
                if (contact) pickup.TryCollect(feet, _collector);
            }
        }

        public void ResetRun()
        {
            foreach (var pickup in _pickups)
                if (pickup != null) pickup.Retire();
            _recycleCursor = 0;
        }

        void OnDisable() => ResetRun();

        void OnDestroy()
        {
            ResetRun();
            if (_root != null)
            {
                if (Application.isPlaying) Destroy(_root.gameObject);
                else DestroyImmediate(_root.gameObject);
            }
            _pickups.Clear();
            _art?.Dispose();
            _art = null;
        }

        static bool TryDefaultPlacement(Vector3 death, out Vector3 ground)
        {
            ground = default;
            var terrain = WorldTerrain.Instance;
            if (terrain == null || terrain.Map == null ||
                !terrain.TryGetWalkablePosition(death, out Vector3 start) ||
                Mathf.Abs(start.y - death.y) > 1.5f) return false;
            var traversal = terrain.Traversal;
            if (traversal != null && !traversal.CanOccupy(start, .3f)) return false;

            for (int attempt = 0; attempt < 25; attempt++)
            {
                float angle = (attempt - 1) % 8 * Mathf.PI * .25f;
                float radius = attempt == 0 ? 0f : (1 + (attempt - 1) / 8) * .4f;
                var target = start + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (traversal != null && (traversal.ZoneAt(start) != traversal.ZoneAt(target) ||
                    !traversal.CanTraverse(start, target, .3f))) continue;
                int steps = Mathf.Max(1, Mathf.CeilToInt(radius / .15f));
                Vector3 previous = start;
                bool reachable = true;
                for (int step = 1; step <= steps; step++)
                {
                    var sample = Vector3.Lerp(start, target, (float)step / steps);
                    if (!terrain.TryGetWalkablePosition(sample, out Vector3 surface, .3f) ||
                        Mathf.Abs(surface.y - previous.y) > .55f)
                    {
                        reachable = false;
                        break;
                    }
                    previous = surface;
                }
                if (!reachable) continue;
                ground = previous;
                return true;
            }
            return false;
        }

        static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
    }
}
