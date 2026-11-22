using System;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>
    /// A stationary physical-contact pickup. Its owner advances it even outside the camera
    /// or an unloaded terrain chunk. No attraction, trigger dependency or movement sweep.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundPickup : MonoBehaviour
    {
        SpriteRenderer _icon;
        SpriteRenderer _ring;
        Transform _visual;
        double _age;
        float _visualOffset;
        bool _collecting;
        int _activation;

        public GroundPickupKind Kind { get; private set; }
        public TemporaryEffect Effect { get; private set; }
        public int BonusPoints { get; private set; }
        public Vector3 GroundPosition { get; private set; }
        public bool IsAvailable { get; private set; }
        public float RemainingLifetime => IsAvailable ? (float)Math.Max(0d, DropDefinitions.Lifetime - _age) : 0f;
        internal double Age => _age;
        internal int SpawnFrame { get; private set; }

        internal void BuildVisual(Sprite ringSprite)
        {
            var icon = new GameObject("DropSymbol");
            icon.transform.SetParent(transform, false);
            _visual = icon.transform;
            _icon = icon.AddComponent<SpriteRenderer>();
            _icon.sortingOrder = 16;

            var ring = new GameObject("GroundRing");
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.up * .025f;
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _ring = ring.AddComponent<SpriteRenderer>();
            _ring.sprite = ringSprite;
            _ring.sharedMaterial = _icon.sharedMaterial; // Borrow the default sprite material; never clone it.
            _ring.sortingOrder = 15;
        }

        internal void Activate(Vector3 ground, GroundPickupKind kind, TemporaryEffect effect,
            int bonusPoints, Sprite sprite, Color color, float visualOffset, Camera camera)
        {
            _activation++;
            GroundPosition = ground;
            Kind = kind;
            Effect = effect;
            BonusPoints = bonusPoints;
            _age = 0d;
            SpawnFrame = Time.frameCount;
            _collecting = false;
            IsAvailable = true;
            transform.position = ground;
            _visualOffset = visualOffset;
            _icon.sprite = sprite;
            _icon.color = UnityEngine.Color.white;
            _ring.color = color;
            _icon.enabled = true;
            _ring.enabled = true;
            gameObject.SetActive(true);
            UpdateVisual(camera);
        }

        internal void Retire()
        {
            IsAvailable = false;
            gameObject.SetActive(false);
        }

        internal void Tick(double gameplaySeconds, Camera camera)
        {
            if (!IsAvailable) return;
            _age += gameplaySeconds;
            if (_age >= DropDefinitions.Lifetime)
            {
                Retire();
                return;
            }
            UpdateVisual(camera);
        }

        void UpdateVisual(Camera camera)
        {
            Vector3 right = Vector3.right;
            if (camera != null)
            {
                Vector3 forward = -camera.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > .0001f)
                    _visual.rotation = Quaternion.LookRotation(forward, Vector3.up);
                right = camera.transform.right;
                right.y = 0f;
                right.Normalize();
            }
            _visual.localPosition = right * _visualOffset +
                Vector3.up * (.32f + Mathf.Sin((float)_age * 5f) * .035f);
            bool visible = _age < 4d || (int)((_age - 4d) * 10d) % 2 == 0;
            _icon.enabled = visible;
            _ring.enabled = visible;
        }

        /// <summary>Tests the current feet/body footprint only, so an R teleport cannot sweep up loot.</summary>
        public static bool IsInContact(Vector3 playerFeet, Vector3 ground)
        {
            Vector3 delta = playerFeet - ground;
            if (float.IsNaN(delta.y) || delta.y < -DropDefinitions.MaxFeetBelowGround ||
                delta.y > DropDefinitions.MaxFeetAboveGround) return false;
            return delta.x * delta.x + delta.z * delta.z <=
                DropDefinitions.ContactRadius * DropDefinitions.ContactRadius;
        }

        internal bool TryCollect(Vector3 playerFeet, Func<GroundPickup, bool> collect)
        {
            if (!IsAvailable || _collecting || !IsInContact(playerFeet, GroundPosition)) return false;
            int activation = _activation;
            _collecting = true;
            bool accepted;
            try { accepted = collect(this); }
            finally { _collecting = false; }
            // A reward event may reset the run or reuse a pooled entry.
            if (accepted && activation == _activation) Retire();
            return accepted;
        }
    }
}
