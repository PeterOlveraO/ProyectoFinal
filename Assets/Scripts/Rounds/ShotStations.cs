using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.Terrain;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>Seven buildable arsenals, including buying Basic again. Positions describe ground.</summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class ShotStations : MonoBehaviour
    {
        const float InteractionRadius = 3.2f;
        IReadOnlyList<Vector3> _positions = System.Array.AsReadOnly(new Vector3[0]);
        public IReadOnlyList<Vector3> Positions => _positions;
        public const int OfferCount = ShotDefinitions.Count;
        static readonly ShotType[] Types = { ShotType.Burst, ShotType.Fan, ShotType.Piercing, ShotType.Basic,
            ShotType.Precision, ShotType.Boomerang, ShotType.Explosive };
        static readonly StationIcon[] Icons = { StationIcon.Burst, StationIcon.Fan, StationIcon.Piercing,
            StationIcon.Basic, StationIcon.Precision, StationIcon.Boomerang, StationIcon.Explosive };
        static readonly int[] Zones = { 2, 3, 4, 1, 6, 8, 7 };
        public static ShotType OfferType(int offer) => offer >= 0 && offer < OfferCount ? Types[offer] : (ShotType)(-1);
        public static int OfferZone(int offer) => offer >= 0 && offer < OfferCount ? Zones[offer] : 0;
        public static StationIcon OfferIcon(int offer) => Icons[offer];
        public static int BuildPrice(int offer)
        {
            switch (offer)
            {
                case 0: return 110;
                case 1: return 150;
                case 2: return 200;
                case 3: return 80;
                case 4: return 180;
                case 5: return 200;
                case 6: return 220;
                default: return 0;
            }
        }
        readonly TycoonUnlocks _construction = new TycoonUnlocks(OfferCount);
        readonly TycoonPlatform[] _platforms = new TycoonPlatform[OfferCount];

        public bool IsBuilt(int index) => _construction.IsBuilt(index);
        public bool Build(int index)
        {
            var round = RoundManager.Instance;
            var player = PlayerSphereController.Instance;
            if (!GameFlow.Playing || round == null || !round.IsRunActive || player == null || !player.IsAlive ||
                index < 0 || index >= OfferCount || NearbyOffer(player.transform.position) != index) return false;
            if (!_construction.TryBuild(index, BuildPrice(index), round.SpendPoints)) return false;
            _platforms[index]?.SetBuilt(true);
            return true;
        }

        public void ResetRun()
        {
            _construction.ResetRun();
            foreach (var platform in _platforms) platform?.SetBuilt(false);
        }

        void Start()
        {
            var terrain = WorldTerrain.Instance;
            if (terrain == null || terrain.Map == null)
            {
                Debug.LogError("[ShotStations] No hay un WorldTerrain generado para colocar los arsenales.", this);
                return;
            }
            var drinks = GetComponent<DrinkStations>();
            Vector3[] positions;
            try
            {
                positions = WorldShopPlacement.FindShotPositions(terrain, drinks != null ? drinks.Positions : null);
            }
            catch (System.InvalidOperationException exception)
            {
                Debug.LogError("[ShotStations] " + exception.Message, this);
                return;
            }
            _positions = System.Array.AsReadOnly(positions);
            for (int i = 0; i < positions.Length; i++) CreateStand(i, positions[i]);
        }

        void CreateStand(int index, Vector3 ground)
        {
            var type = OfferType(index);
            var productIcon = Icons[index];
            var platform = new TycoonPlatform(transform, ground, ShotDefinitions.Name(type),
                BuildPrice(index), ShotDefinitions.Price(type), ShotDefinitions.Color(type), productIcon, true);
            _platforms[index] = platform;
            platform.SetZone(OfferZone(index));
            var root = platform.Vendor.gameObject;
            root.AddComponent<BillboardSprite>().SetContinuous(false);

            var icon = root.AddComponent<SpriteRenderer>();
            icon.sprite = RetroWorldArt.Terminal;
            root.transform.localScale = Vector3.one * 1.8f;
            icon.sortingOrder = 12;

            var symbol = new GameObject("Symbol");
            symbol.transform.SetParent(root.transform, false);
            symbol.transform.localPosition = new Vector3(0, .065f, -.02f);
            symbol.transform.localScale = Vector3.one * .63f;
            var pictogram = symbol.AddComponent<SpriteRenderer>();
            pictogram.sprite = StationIconArt.Get(productIcon);
            pictogram.sortingOrder = 13;
            platform.RegisterFog(WorldTerrain.Instance != null ? WorldTerrain.Instance.ZoneFog : null);

        }

        public int NearbyOffer(Vector3 position)
        {
            int nearest = -1;
            float best = InteractionRadius * InteractionRadius;
            for (int i = 0; i < _positions.Count; i++)
            {
                if (!WorldShopPlacement.CanAccessZone(OfferZone(i))) continue;
                Vector3 delta = _positions[i] - position;
                if (Mathf.Abs(delta.y) > 1.9f) continue;
                delta.y = 0f;
                if (delta.sqrMagnitude >= best) continue;
                best = delta.sqrMagnitude;
                nearest = i;
            }
            return nearest;
        }
    }
}
