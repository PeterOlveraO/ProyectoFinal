using UnityEngine;

namespace Game.UI
{
    /// <summary>Unscaled, looping pool preview. It never selects or awards a gacha result.</summary>
    public sealed class GachaRoulette : MonoBehaviour
    {
        const int SlotCount = 7;
        const float HoldSeconds = 1.15f, SlideSeconds = .45f;
        readonly RectTransform[] _slots = new RectTransform[SlotCount];
        readonly UnityEngine.UI.Image[] _portraits = new UnityEngine.UI.Image[SlotCount];
        Sprite[] _items = System.Array.Empty<Sprite>();
        RectTransform _root;
        float _elapsed;
        public int FeaturedIndex { get; private set; }

        public void Bind(Sprite[] items)
        {
            EnsureSlots();
            bool same = items.Length == _items.Length;
            for (int i = 0; same && i < items.Length; i++) same = items[i] == _items[i];
            if (!same)
            {
                _items = (Sprite[])items.Clone();
                FeaturedIndex = 0;
                _elapsed = 0;
                RefreshSprites();
            }
            Arrange(0);
        }

        void EnsureSlots()
        {
            if (_root != null) return;
            _root = (RectTransform)transform;
            gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            for (int i = 0; i < SlotCount; i++)
            {
                var portrait = UIFactory.PortraitStage(transform, "Candidate" + i, null);
                _portraits[i] = portrait;
                _slots[i] = (RectTransform)portrait.transform.parent;
                UIFactory.Place(_slots[i], new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                    Vector2.zero, new Vector2(120, 140));
            }
            // A small marker identifies the featured sprite, without introducing names.
            var marker = UIFactory.CreateImage("FeaturedMarker", transform, null, UIFactory.Sun);
            UIFactory.Place(marker.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 7), new Vector2(30, 3));
        }

        void OnEnable()
        {
            _elapsed = 0;
            if (_root != null) Arrange(0);
        }

        void Update()
        {
            if (_root == null || _items.Length == 0) return;
            if (_items.Length > 1)
            {
                _elapsed += Time.unscaledDeltaTime;
                if (_elapsed >= HoldSeconds + SlideSeconds)
                {
                    _elapsed %= HoldSeconds + SlideSeconds;
                    FeaturedIndex = (FeaturedIndex + 1) % _items.Length;
                    RefreshSprites();
                }
            }
            float slide = Mathf.Clamp01((_elapsed - HoldSeconds) / SlideSeconds);
            Arrange(slide * slide * (3 - 2 * slide));
        }

        void RefreshSprites()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var image = _portraits[i];
                image.sprite = _items.Length == 0 ? null :
                    _items[(FeaturedIndex + i - SlotCount / 2 + _items.Length * SlotCount) % _items.Length];
                image.enabled = image.sprite != null;
            }
        }

        void Arrange(float slide)
        {
            float step = _root.rect.width * .27f;
            float width = Mathf.Min(132, _root.rect.width * .30f);
            for (int i = 0; i < SlotCount; i++)
            {
                float offset = i - SlotCount / 2 - slide;
                float emphasis = Mathf.Clamp01(1 - Mathf.Abs(offset));
                _slots[i].sizeDelta = new Vector2(width, 146);
                _slots[i].anchoredPosition = new Vector2(offset * step, 4);
                _slots[i].localScale = Vector3.one * Mathf.Lerp(.72f, 1f, emphasis);
                _portraits[i].color = Color.Lerp(new Color(.67f, .76f, .76f, .8f), Color.white, emphasis);
            }
        }
    }
}
