using Game.Terrain;
using UnityEngine;

namespace Game.Rendering
{
    /// <summary>Small pixel shadow projected onto the same height map as the actor.</summary>
    public sealed class GroundContactShadow : MonoBehaviour
    {
        SpriteRenderer _renderer;
        Transform _visual;

        public static void Attach(Transform actor, float width)
        {
            var shadow = actor.GetComponent<GroundContactShadow>();
            if (shadow == null) shadow = actor.gameObject.AddComponent<GroundContactShadow>();
            if (shadow._renderer == null)
            {
                var go = new GameObject("Ground Contact");
                go.transform.SetParent(actor, false);
                shadow._visual = go.transform;
                shadow._renderer = go.AddComponent<SpriteRenderer>();
                shadow._renderer.sprite = RetroWorldArt.Shadow;
                shadow._renderer.sortingOrder = -2;
            }
            shadow._visual.localScale = new Vector3(width, width, 1);
        }

        void LateUpdate()
        {
            if (_renderer == null) return;
            var terrain = WorldTerrain.Instance;
            if (terrain == null || !terrain.TryGetWalkablePosition(transform.position, out var ground))
            {
                _renderer.enabled = false;
                return;
            }
            _renderer.enabled = true;
            _visual.position = ground + Vector3.up * .025f;
            _visual.rotation = Quaternion.Euler(90, 0, 0);
        }
    }
}
