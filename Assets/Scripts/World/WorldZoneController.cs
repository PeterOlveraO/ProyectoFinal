using System;
using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.Rounds;
using Game.Terrain;
using TMPro;
using UnityEngine;

namespace Game.World
{
    /// <summary>Run economy + nearby gate authorization. RoundManager explicitly owns run resets.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldZoneController : MonoBehaviour
    {
        sealed class Gate
        {
            public int A, B, Offer;
            public GameObject Root;
            public TextMeshPro Label;
            public Vector3 Position;
        }
        public const float InteractionRadius = 3.2f;
        readonly RunZoneUnlocks _unlocks = new RunZoneUnlocks();
        readonly List<Gate> _gates = new List<Gate>(12);
        RoundManager _rounds;
        WorldTerrain _terrain;
        bool _built, _buying;
        public static WorldZoneController Instance { get; private set; }
        public event Action Changed;
        public RunZoneUnlocks Unlocks => _unlocks;
        public int UnlockedCount => _unlocks.UnlockedCount;
        public bool IsUnlocked(int zoneId) => _unlocks.IsUnlocked(zoneId);
        public int Cost(int zoneId) => WorldZoneLayout.Cost(zoneId);
        public bool Enabled => _terrain != null && _terrain.Traversal != null;

        void Awake()
        {
            Instance = this;
            _unlocks.Changed += OnUnlocksChanged;
        }

        public void Bind(RoundManager rounds)
        {
            _rounds = rounds;
            AttachTerrain();
        }

        void Start()
        {
            if (_rounds == null) _rounds = GetComponent<RoundManager>();
            AttachTerrain();
        }

        void AttachTerrain()
        {
            if (_terrain == null) _terrain = WorldTerrain.Instance;
            if (!Enabled) return;
            _terrain.Traversal.SetZoneAccess(_unlocks);
            if (!_built)
            {
                BuildGates();
                _built = true;
            }
            RefreshGates();
            if (_terrain.ZoneFog != null) _terrain.ZoneFog.Refresh();
        }

        /// <summary>Call for a NEW run only, never at a wave transition or pause.</summary>
        public void StartRun() => ResetRun();
        public void ResetRun()
        {
            if (_buying) return;
            _unlocks.ResetRun();
            AttachTerrain();
        }

        public bool CanBuy(int zoneId)
        {
            var player = PlayerSphereController.Instance;
            return Enabled && GameFlow.Playing && Time.timeScale > 0f && _rounds != null &&
                _rounds.IsRunActive && player != null && player.IsAlive &&
                _unlocks.CanUnlock(zoneId) && _rounds.Points >= Cost(zoneId) &&
                NearbyOffer(player.transform.position) == zoneId;
        }

        public bool Buy(int zoneId)
        {
            if (_buying || !CanBuy(zoneId)) return false;
            _buying = true;
            try { return _unlocks.TryUnlock(zoneId, _rounds.SpendPoints); }
            finally { _buying = false; }
        }

        /// <summary>Purchase zone ID (2..9), or -1. Checks accessible side and height as well as radius.</summary>
        public int NearbyOffer(Vector3 position)
        {
            if (!Enabled || !_terrain.Traversal.CanOccupy(position)) return -1;
            int nearest = -1;
            float best = InteractionRadius * InteractionRadius;
            foreach (Gate gate in _gates)
            {
                if (gate.Offer < 2 || !_unlocks.CanUnlock(gate.Offer)) continue;
                Vector3 delta = gate.Position - position;
                if (Mathf.Abs(delta.y) > 1.9f) continue;
                delta.y = 0;
                if (delta.sqrMagnitude >= best) continue;
                best = delta.sqrMagnitude;
                nearest = gate.Offer;
            }
            return nearest;
        }

        public bool TryGetOfferPosition(int zoneId, out Vector3 position)
        {
            foreach (Gate gate in _gates)
                if (gate.Offer == zoneId) { position = gate.Position; return true; }
            position = default;
            return false;
        }

        void OnUnlocksChanged()
        {
            RefreshGates();
            if (_terrain != null && _terrain.ZoneFog != null) _terrain.ZoneFog.Refresh();
            Changed?.Invoke();
        }

        void RefreshGates()
        {
            if (!Enabled) return;
            foreach (Gate gate in _gates)
            {
                bool a = IsUnlocked(gate.A), b = IsUnlocked(gate.B);
                gate.Offer = a == b ? -1 : a ? gate.B : gate.A;
                gate.Root.SetActive(gate.Offer > 0);
                if (gate.Offer < 0) continue;
                int open = a ? gate.A : gate.B;
                _terrain.Traversal.Layout.TryGetGate(open, gate.Offer, out Vector3 boundary, out Vector3 direction);
                // Sign and purchase pad stay on the accessible side of the black frontier.
                Vector3 candidate = boundary - direction * 2.4f;
                if (!_terrain.TryGetWalkablePosition(candidate, out Vector3 ground, .5f))
                {
                    gate.Offer = -1;
                    gate.Root.SetActive(false);
                    continue;
                }
                gate.Position = ground;
                gate.Root.transform.position = ground;
                gate.Label.text = "<color=#E9C46A><size=80%>EXPANDIR</size></color>\nZONA " +
                    gate.Offer + "\n" + Cost(gate.Offer) + " PT";
            }
        }

        void BuildGates()
        {
            for (int a = 1; a <= 9; a++)
            for (int b = a + 1; b <= 9; b++)
            {
                if (!WorldZoneLayout.AreAdjacent(a, b)) continue;
                var root = new GameObject("ZoneGate_" + a + "_" + b);
                root.transform.SetParent(transform, false);
                SpriteRenderer pad = Sprite(root.transform, "Foundation", RetroWorldArt.Pad,
                    new Vector3(0, .035f, 0), new Vector3(2.5f, 2.5f, 1));
                pad.transform.localRotation = Quaternion.Euler(90, 0, 0);
                SpriteRenderer corners = Sprite(root.transform, "ExpansionCorners", RetroWorldArt.Corners,
                    new Vector3(0, .045f, 0), new Vector3(2.5f, 2.5f, 1));
                corners.transform.localRotation = Quaternion.Euler(90, 0, 0);
                corners.color = new Color32(233, 196, 106, 255);
                var marker = new GameObject("GateMarker");
                marker.transform.SetParent(root.transform, false);
                marker.transform.localPosition = Vector3.up * .7f;
                marker.AddComponent<BillboardSprite>();
                Sprite(marker.transform, "Timber", RetroWorldArt.Construction, Vector3.zero, Vector3.one * 1.3f);
                var sign = new GameObject("ZoneAndPrice");
                sign.transform.SetParent(root.transform, false);
                sign.transform.localPosition = Vector3.up * 2.7f;
                sign.AddComponent<BillboardSprite>();
                Sprite(sign.transform, "Frame", RetroWorldArt.Sign, Vector3.zero, new Vector3(2.05f, 1.7f, 1));
                var textObject = new GameObject("Label");
                textObject.transform.SetParent(sign.transform, false);
                textObject.transform.localPosition = new Vector3(0, 0, -.025f);
                textObject.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var label = textObject.AddComponent<TextMeshPro>();
                label.fontSize = 2.1f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(3.7f, 1.2f);
                label.color = RetroWorldArt.Cream;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.GetComponent<MeshRenderer>().sortingOrder = 14;
                root.AddComponent<StationPresentation>().Initialize(sign);
                _gates.Add(new Gate { A = a, B = b, Offer = -1, Root = root, Label = label });
            }
        }

        static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Vector3 position, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return renderer;
        }

        void OnDestroy()
        {
            _unlocks.Changed -= OnUnlocksChanged;
            if (Instance == this) Instance = null;
        }
    }
}
