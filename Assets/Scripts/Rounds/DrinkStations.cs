using System;
using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.Terrain;
using UnityEngine;

namespace Game.Rounds
{
    public enum DrinkType { Power, Vitality, Sprint, Armor, RapidFire, Ballistics, Recovery, Reserve }

    /// <summary>Eight buildable perk machines. One perk per type/run.</summary>
    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    public sealed class DrinkStations : MonoBehaviour
    {
        public const int OfferCount = 8;
        public static readonly string[] Names = { "FUERZA", "VITALIDAD", "SPRINT", "ESCUDO", "RÁFAGA",
            "BALÍSTICA", "RECUPERACIÓN", "RESERVA" };
        // Coste por partida (no se guarda como moneda persistente).
        public static readonly int[] Prices = { 100, 140, 90, 160, 180, 180, 220, 240 };
        public static readonly int[] BuildPrices = { 100, 120, 100, 140, 160, 140, 180, 200 };
        static readonly Color[] Colors =
        {
            new Color(1f, .28f, .22f), new Color(.35f, 1f, .45f),
            new Color(.3f, .8f, 1f), new Color(.65f, .55f, 1f), new Color(1f, .8f, .25f),
            new Color(.3f, .95f, .85f), new Color(.7f, 1f, .55f), new Color(.95f, .65f, 1f)
        };
        static readonly StationIcon[] Icons = { StationIcon.Power, StationIcon.Vitality, StationIcon.Sprint,
            StationIcon.Armor, StationIcon.RapidFire, StationIcon.Ballistics, StationIcon.Recovery, StationIcon.Reserve };
        static readonly int[] Zones = { 1, 1, 2, 4, 5, 3, 5, 9 };
        public static int OfferZone(int index) => index >= 0 && index < OfferCount ? Zones[index] : 0;
        public static StationIcon OfferIcon(int index) => Icons[index];

        readonly Transform[] _stands = new Transform[OfferCount];
        readonly bool[] _purchased = new bool[OfferCount];
        readonly TycoonUnlocks _construction = new TycoonUnlocks(OfferCount);
        readonly TycoonPlatform[] _platforms = new TycoonPlatform[OfferCount];
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
                Debug.LogError("[DrinkStations] No hay un WorldTerrain generado; no se pudieron colocar los puestos.", this);
                return;
            }

            Vector3[] positions;
            try
            {
                positions = WorldShopPlacement.FindDrinkPositions(_terrain);
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
            var platform = new TycoonPlatform(transform, ground, Names[index],
                BuildPrices[index], Prices[index], Colors[index], Icons[index]);
            platform.SetZone(OfferZone(index));
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
            platform.RegisterFog(WorldTerrain.Instance != null ? WorldTerrain.Instance.ZoneFog : null);
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
                if (!WorldShopPlacement.CanAccessZone(OfferZone(i))) continue;
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
            _platforms[index]?.SetPurchased();
            if ((DrinkType)index == DrinkType.Vitality) player.Heal(50);
            return true;
        }
    }
}
