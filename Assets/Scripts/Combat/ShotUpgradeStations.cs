using System;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.Rounds;
using Game.Terrain;
using UnityEngine;

namespace Game.Upgrades
{
    public enum ShotUpgrade { Rapid, Double, Triple, Area, Pierce, Power }

    /// <summary>Seis mejoras acumulables, una compra de cada tipo por partida.</summary>
    [DisallowMultipleComponent]
    public sealed class ShotUpgradeStations : MonoBehaviour
    {
        public static readonly string[] Names = { "RÁPIDO", "DOBLE", "TRIPLE", "ÁREA", "PERFORANTE", "POTENCIA" };
        // Ronda 1: ~128 puntos; ronda 10: ~452. Requieren ahorrar varias rondas.
        public static readonly int[] Prices = { 550, 450, 700, 750, 650, 600 };
        static readonly Color[] Colors = {
            new Color(1f, .5f, .2f), new Color(1f, .85f, .2f), new Color(1f, .25f, .3f),
            new Color(.3f, .85f, 1f), new Color(.4f, 1f, .5f), new Color(.75f, .5f, 1f)
        };

        readonly Transform[] _stands = new Transform[6];
        readonly bool[] _owned = new bool[6];
        bool _wasActive;
        bool _built;

        public bool Has(int index) => index >= 0 && index < _owned.Length && _owned[index];
        public bool Has(ShotUpgrade upgrade) => Has((int)upgrade);
        /// <summary>Llamar al iniciar cada partida, incluidos reintentos.</summary>
        public void ResetRun() => Array.Clear(_owned, 0, _owned.Length);

        void Update()
        {
            if (!_built && WorldTerrain.Instance != null && WorldTerrain.Instance.Map != null) BuildStations();
            var round = RoundManager.Instance;
            bool active = round != null && round.IsRunActive;
            if (active && !_wasActive) ResetRun();
            _wasActive = active;
        }

        void BuildStations()
        {
            var terrain = WorldTerrain.Instance;
            var map = terrain.Map;
            Vector3 spawn = terrain.SpawnPosition;
            float radius = Mathf.Min(map.Width, map.Depth) * terrain.BlockSize * .3f;
            radius = Mathf.Max(8f, radius);
            for (int i = 0; i < _stands.Length; i++)
            {
                bool found = false;
                for (int attempt = 0; attempt < 96; attempt++)
                {
                    float angle = (i * 60f + (attempt % 12 - 5.5f) * 3f) * Mathf.Deg2Rad;
                    float distance = radius * (1f - (attempt / 12) * .065f);
                    Vector3 candidate = spawn + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    Vector3 local = candidate - terrain.transform.position;
                    int x = Mathf.FloorToInt(local.x / terrain.BlockSize);
                    int z = Mathf.FloorToInt(local.z / terrain.BlockSize);
                    if (!map.Contains(x, z) || terrain.IsLakeAtWorld(candidate)) continue;
                    Vector3 ground = terrain.BlockTopCenter(x, z);
                    bool accessible = false;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                            if ((dx != 0 || dz != 0) && map.Contains(x + dx, z + dz) &&
                                Mathf.Abs(map.Get(x + dx, z + dz) - map.Get(x, z)) <= 1) accessible = true;
                    if (!accessible) continue;
                    CreateStand(i, ground);
                    found = true;
                    break;
                }
                if (!found) Debug.LogWarning("[ShotUpgradeStations] Sin ubicación para " + Names[i]);
            }
            _built = true;
        }

        void CreateStand(int index, Vector3 ground)
        {
            // No heredar movimiento del jugador.
            var root = new GameObject("MejoraDisparo_" + Names[index]);
            root.transform.position = ground + Vector3.up * 1f;
            _stands[index] = root.transform;
            var sprite = root.AddComponent<SpriteRenderer>();
            sprite.sprite = ProceduralSprites.RoundedRect;
            sprite.drawMode = SpriteDrawMode.Sliced;
            sprite.size = new Vector2(1.2f, 1.8f);
            sprite.color = Colors[index];
            sprite.sortingOrder = 12;
            root.AddComponent<BillboardSprite>().SetContinuous(true);
            var emblem = new GameObject("Emblema");
            emblem.transform.SetParent(root.transform, false);
            var mark = emblem.AddComponent<SpriteRenderer>();
            mark.sprite = ProceduralSprites.Circle;
            mark.color = Color.white;
            mark.sortingOrder = 13;
            emblem.transform.localScale = Vector3.one * .45f;
        }

        public int NearbyOffer(Vector3 position)
        {
            int nearest = -1;
            float best = 3.2f * 3.2f;
            for (int i = 0; i < _stands.Length; i++)
            {
                if (_stands[i] == null) continue;
                Vector3 delta = _stands[i].position - position;
                if (Mathf.Abs(delta.y) > 3f) continue;
                delta.y = 0f;
                if (delta.sqrMagnitude >= best) continue;
                best = delta.sqrMagnitude;
                nearest = i;
            }
            return nearest;
        }

        public bool Buy(int index)
        {
            var player = PlayerSphereController.Instance;
            var round = RoundManager.Instance;
            if (!GameFlow.Playing || round == null || !round.IsRunActive || player == null || !player.IsAlive ||
                index < 0 || index >= _stands.Length || Has(index) || NearbyOffer(player.transform.position) != index)
                return false;
            if (!round.SpendPoints(Prices[index])) return false;
            _owned[index] = true;
            return true;
        }

        void OnDestroy()
        {
            foreach (var stand in _stands) if (stand != null) Destroy(stand.gameObject);
        }
    }
}
