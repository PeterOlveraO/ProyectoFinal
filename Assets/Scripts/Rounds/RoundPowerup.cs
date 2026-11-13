using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>Objeto recogible temporal generado al derrotar enemigos de ronda.</summary>
    public sealed class RoundPowerup : MonoBehaviour
    {
        public enum Kind { DoublePoints, Shield, SkipRound }

        const float Lifetime = 20f;
        const float PickupRadius = 1.25f;
        RoundManager _owner;
        Kind _kind;
        float _remaining = Lifetime;

        public static void Spawn(RoundManager owner, Vector3 position, Kind kind)
        {
            var go = new GameObject("Powerup_" + kind);
            go.transform.SetParent(owner.transform, true);
            go.transform.position = position + Vector3.up * 0.8f;
            var pickup = go.AddComponent<RoundPowerup>();
            pickup._owner = owner;
            pickup._kind = kind;
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = ProceduralSprites.RoundedRect;
            sprite.color = kind == Kind.DoublePoints ? Color.yellow :
                           kind == Kind.Shield ? Color.cyan : new Color(1f, 0.35f, 0.9f);
            sprite.sortingOrder = 25;
            go.transform.localScale = Vector3.one * 0.6f;
            go.AddComponent<BillboardSprite>().SetContinuous(true);
        }

        void Update()
        {
            if (_owner == null || !_owner.IsRunActive) { Destroy(gameObject); return; }
            if (GameFlow.Blocked) return;
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f) { Destroy(gameObject); return; }

            var player = PlayerSphereController.Instance;
            if (player == null || !player.IsAlive) return;
            Vector3 delta = player.transform.position - transform.position;
            if (Mathf.Abs(delta.y) > 2f) return;
            delta.y = 0f;
            if (delta.sqrMagnitude <= PickupRadius * PickupRadius && _owner.CollectPowerup(_kind))
                Destroy(gameObject);
        }
    }
}
