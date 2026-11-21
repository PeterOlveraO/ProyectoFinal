using System.Collections.Generic;
using Game.Combat;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.Terrain;
using TMPro;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>Four buildable arsenals, including buying Basic again. Positions describe ground.</summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class ShotStations : MonoBehaviour
    {
        const float InteractionRadius = 3.2f;
        IReadOnlyList<Vector3> _positions = System.Array.AsReadOnly(new Vector3[0]);
        public IReadOnlyList<Vector3> Positions => _positions;
        public const int OfferCount = 4;
        public static ShotType OfferType(int offer) => offer == 3 ? ShotType.Basic : (ShotType)(offer + 1);
        public static int BuildPrice(int offer)
        {
            switch (offer)
            {
                case 0: return 110;
                case 1: return 150;
                case 2: return 200;
                case 3: return 80;
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
                positions = WorldShopPlacement.FindPositions(terrain, OfferCount, 104729, drinks != null ? drinks.Positions : null);
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
            var platform = new TycoonPlatform(transform, ground, "ARSENAL " + ShotDefinitions.Name(type),
                BuildPrice(index), ShotDefinitions.Price(type), ShotDefinitions.Color(type));
            _platforms[index] = platform;
            var root = platform.Vendor.gameObject;
            root.AddComponent<BillboardSprite>().SetContinuous(false);

            var icon = root.AddComponent<SpriteRenderer>();
            icon.sprite = RetroWorldArt.Terminal;
            root.transform.localScale = Vector3.one * 1.8f;
            icon.sortingOrder = 12;

            var symbol = new GameObject("Symbol");
            symbol.transform.SetParent(root.transform, false);
            // TMP's readable face is -Z, opposite to the sprite billboard's forward.
            symbol.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var text = symbol.AddComponent<TextMeshPro>();
            symbol.transform.localPosition = new Vector3(0, .065f, -.02f);
            text.text = type == ShotType.Burst ? "III" : type == ShotType.Fan ? "V" :
                type == ShotType.Piercing ? ">>" : ">";
            text.fontSize = 3.5f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.Lerp(ShotDefinitions.Color(type), RetroWorldArt.Cream, .4f);
            text.rectTransform.sizeDelta = new Vector2(1.2f, 1.3f);
            text.GetComponent<MeshRenderer>().sortingOrder = 13;

        }

        public int NearbyOffer(Vector3 position)
        {
            int nearest = -1;
            float best = InteractionRadius * InteractionRadius;
            for (int i = 0; i < _positions.Count; i++)
            {
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
