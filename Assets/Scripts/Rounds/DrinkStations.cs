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

    /// <summary>Cinco puestos de bebidas provisionales en el mapa. Una compra por tipo y partida.</summary>
    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    public sealed class DrinkStations : MonoBehaviour
    {
        public static readonly string[] Names = { "FUERZA", "VITALIDAD", "SPRINT", "ESCUDO", "RÁFAGA" };
        // Coste por partida (no se guarda como moneda persistente).
        public static readonly int[] Prices = { 30, 45, 35, 45, 60 };
        static readonly Color[] Colors =
        {
            new Color(1f, .28f, .22f), new Color(.35f, 1f, .45f),
            new Color(.3f, .8f, 1f), new Color(.65f, .55f, 1f), new Color(1f, .8f, .25f)
        };

        readonly Transform[] _stands = new Transform[5];
        readonly bool[] _purchased = new bool[5];
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
            var root = new GameObject("Bebida_" + Names[index]);
            root.transform.SetParent(transform, true);
            root.transform.position = ground + Vector3.up * .85f;
            _stands[index] = root.transform;

            var bottle = new GameObject("Botella");
            bottle.transform.SetParent(root.transform, false);
            var renderer = bottle.AddComponent<SpriteRenderer>();
            renderer.sprite = ProceduralSprites.RoundedRect;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(.7f, 1.4f);
            renderer.color = Colors[index];
            renderer.sortingOrder = 12;
            bottle.AddComponent<BillboardSprite>().SetContinuous(true);

            var cap = new GameObject("Tapón");
            cap.transform.SetParent(bottle.transform, false);
            cap.transform.localPosition = new Vector3(0f, .78f, 0f);
            var capRenderer = cap.AddComponent<SpriteRenderer>();
            capRenderer.sprite = ProceduralSprites.RoundedRect;
            capRenderer.drawMode = SpriteDrawMode.Sliced;
            capRenderer.size = new Vector2(.42f, .22f);
            capRenderer.color = Color.white;
            capRenderer.sortingOrder = 13;
        }

        public bool Has(DrinkType type) => _purchased[(int)type];

        public void ResetRun()
        {
            for (int i = 0; i < _purchased.Length; i++) _purchased[i] = false;
        }

        public int NearbyOffer(Vector3 position)
        {
            int nearest = -1;
            float best = 3.2f * 3.2f;
            for (int i = 0; i < _stands.Length; i++)
            {
                if (_stands[i] == null) continue;
                Vector3 delta = _stands[i].position - position;
                delta.y = 0;
                if (delta.sqrMagnitude >= best) continue;
                best = delta.sqrMagnitude;
                nearest = i;
            }
            return nearest;
        }

        public bool Buy(int index)
        {
            if (!GameFlow.Playing || index < 0 || index >= _stands.Length || _purchased[index]) return false;
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
