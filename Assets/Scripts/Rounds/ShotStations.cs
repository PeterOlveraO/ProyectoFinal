using System.Collections.Generic;
using Game.Combat;
using Game.Rendering;
using Game.Terrain;
using TMPro;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>Three offers, placed after drinks. Positions always describe ground, not sprite pivots.</summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class ShotStations : MonoBehaviour
    {
        const float InteractionRadius = 3.2f;
        IReadOnlyList<Vector3> _positions = System.Array.AsReadOnly(new Vector3[0]);
        public IReadOnlyList<Vector3> Positions => _positions;

        void Start()
        {
            var terrain = WorldTerrain.Instance;
            if (terrain == null || terrain.Map == null)
            {
                Debug.LogError("[ShotStations] No hay un WorldTerrain generado para colocar los tres puestos.", this);
                return;
            }
            var drinks = GetComponent<DrinkStations>();
            Vector3[] positions;
            try
            {
                positions = WorldShopPlacement.FindPositions(terrain, 3, 104729, drinks != null ? drinks.Positions : null);
            }
            catch (System.InvalidOperationException exception)
            {
                Debug.LogError("[ShotStations] " + exception.Message, this);
                return;
            }
            _positions = System.Array.AsReadOnly(positions);
            for (int i = 0; i < positions.Length; i++) CreateStand((ShotType)(i + 1), positions[i]);
        }

        void CreateStand(ShotType type, Vector3 ground)
        {
            var root = new GameObject("Disparo_" + ShotDefinitions.Name(type));
            root.transform.SetParent(transform, false);
            root.transform.position = ground + Vector3.up * .9f;
            root.AddComponent<BillboardSprite>().SetContinuous(false);

            var icon = root.AddComponent<SpriteRenderer>();
            icon.sprite = ProceduralSprites.RoundedRect;
            icon.drawMode = SpriteDrawMode.Sliced;
            icon.size = new Vector2(1.2f, 1.4f);
            icon.color = ShotDefinitions.Color(type);
            icon.sortingOrder = 12;

            var symbol = new GameObject("Symbol");
            symbol.transform.SetParent(root.transform, false);
            // TMP's readable face is -Z, opposite to the sprite billboard's forward.
            symbol.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var text = symbol.AddComponent<TextMeshPro>();
            text.text = type == ShotType.Burst ? "III" : type == ShotType.Fan ? "V" : "↑";
            text.fontSize = 6f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.08f, .04f, .16f);
            text.rectTransform.sizeDelta = new Vector2(1.2f, 1.3f);
            text.GetComponent<MeshRenderer>().sortingOrder = 13;

            var labelObject = new GameObject("NameAndPrice");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = Vector3.up * 1.3f;
            labelObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var label = labelObject.AddComponent<TextMeshPro>();
            label.text = ShotDefinitions.Name(type) + "\n" + ShotDefinitions.Price(type) + " PUNTOS";
            label.fontSize = 3.5f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(4.5f, 1.3f);
            label.GetComponent<MeshRenderer>().sortingOrder = 14;
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
