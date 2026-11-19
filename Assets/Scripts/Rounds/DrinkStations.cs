using System;
using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.Terrain;
using UnityEngine;

namespace Game.Rounds
{
    public enum DrinkType { Power, Vitality, Sprint, Armor, RapidFire }

    /// <summary>Five buildable perk machines in the tycoon hub. One perk per type/run.</summary>
    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    public sealed class DrinkStations : MonoBehaviour
    {
        public static readonly string[] Names = { "FUERZA", "VITALIDAD", "SPRINT", "ESCUDO", "RÁFAGA" };
        // Coste por partida (no se guarda como moneda persistente).
        public static readonly int[] Prices = { 30, 45, 35, 45, 60 };
        public static readonly int[] BuildPrices = { 40, 50, 40, 50, 60 };
        static readonly Color[] Colors =
        {
            new Color(1f, .28f, .22f), new Color(.35f, 1f, .45f),
            new Color(.3f, .8f, 1f), new Color(.65f, .55f, 1f), new Color(1f, .8f, .25f)
        };

        readonly Transform[] _stands = new Transform[5];
        readonly bool[] _purchased = new bool[5];
        readonly TycoonUnlocks _construction = new TycoonUnlocks(5);
        readonly TycoonPlatform[] _platforms = new TycoonPlatform[5];
        IReadOnlyList<Vector3> _positions = Array.AsReadOnly(new Vector3[0]);
        WorldTerrain _terrain;

        /// <summary>
        /// Posiciones de suelo de los puestos, en orden de DrinkType. Los consumidores deben
        /// iniciar después de DrinkStations (execution order mayor que 0) antes de reservarlas.
        /// </summary>
        public IReadOnlyList<Vector3> Positions => _positions;

        void Start()
        {
            _terrain = WorldTerrain.Instance;
            if (_terrain == null || _terrain.Map == null)
            {
                Debug.LogError("[DrinkStations] No hay un WorldTerrain generado; no se pudieron colocar los cinco puestos.", this);
                return;
            }

            Vector3[] positions;
            try
            {
                // seedOffset separa determinísticamente este grupo de otros grupos del mismo mapa.
                positions = WorldShopPlacement.FindPositions(_terrain, _stands.Length, 0x51A7);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError("[DrinkStations] " + exception.Message, this);
                return;
            }

            for (int i = 0; i < _stands.Length; i++)
                CreateStand(i, positions[i]);
            _positions = Array.AsReadOnly((Vector3[])positions.Clone());
        }

        void CreateStand(int index, Vector3 ground)
        {
            var platform = new TycoonPlatform(transform, ground, "MÁQUINA " + Names[index],
                BuildPrices[index], Prices[index], Colors[index]);
            _platforms[index] = platform;
            _stands[index] = platform.Vendor;

            var bottle = new GameObject("MaquinaPerk");
            bottle.transform.SetParent(platform.Vendor, false);
            var renderer = bottle.AddComponent<SpriteRenderer>();
            var art = Resources.Load<TycoonMachineArt>("TycoonMachine");
            renderer.sprite = art != null ? art.machine : null;
            if (renderer.sprite == null)
                Debug.LogError("[DrinkStations] Falta Resources/TycoonMachine con maquina.png.", this);
            else
                bottle.transform.localScale = Vector3.one * (2.5f / renderer.sprite.bounds.size.y);
            renderer.color = Color.white;
            renderer.sortingOrder = 12;
            bottle.AddComponent<BillboardSprite>().SetContinuous(true);
        }

        public bool Has(DrinkType type) => (int)type >= 0 && (int)type < _purchased.Length && _purchased[(int)type];
        public bool IsBuilt(int index) => _construction.IsBuilt(index);

        public bool Build(int index)
        {
            if (!CanInteract(index)) return false;
            if (!_construction.TryBuild(index, BuildPrices[index], RoundManager.Instance.SpendPoints)) return false;
            _platforms[index]?.SetBuilt(true);
            return true;
        }

        bool CanInteract(int index)
        {
            if (!GameFlow.Playing || index < 0 || index >= _stands.Length) return false;
            var player = PlayerSphereController.Instance;
            var round = RoundManager.Instance;
            return player != null && player.IsAlive && round != null && round.IsRunActive &&
                NearbyOffer(player.transform.position) == index;
        }

        public void ResetRun()
        {
            for (int i = 0; i < _purchased.Length; i++) _purchased[i] = false;
            _construction.ResetRun();
            foreach (var platform in _platforms) platform?.SetBuilt(false);
        }

        public int NearbyOffer(Vector3 position)
        {
            int nearest = -1;
            float best = 3.2f * 3.2f;
            for (int i = 0; i < _stands.Length; i++)
            {
                if (_stands[i] == null) continue;
                Vector3 ground = i < _positions.Count ? _positions[i] : _stands[i].position;
                Vector3 delta = ground - position;
                if (Mathf.Abs(delta.y) > 1.9f) continue;
                delta.y = 0;
                if (delta.sqrMagnitude >= best) continue;
                best = delta.sqrMagnitude;
                nearest = i;
            }
            return nearest;
        }

        public bool Buy(int index)
        {
            if (!CanInteract(index) || !IsBuilt(index) || _purchased[index]) return false;
            var player = PlayerSphereController.Instance;
            var round = RoundManager.Instance;
            if (player == null || !player.IsAlive || round == null || NearbyOffer(player.transform.position) != index) return false;
            if (!round.SpendPoints(Prices[index])) return false;
            _purchased[index] = true;
            if ((DrinkType)index == DrinkType.Vitality) player.Heal(50);
            return true;
        }
    }
}
