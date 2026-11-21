using Game.Rendering;
using TMPro;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>A walk-through stone foundation, construction marker and revealed vendor.</summary>
    public sealed class TycoonPlatform
    {
        readonly GameObject _vendor;
        readonly GameObject _construction;
        readonly TextMeshPro _label;
        readonly TextMeshPro _symbol;
        readonly SpriteRenderer _corners;
        readonly StationPresentation _presentation;
        readonly string _name;
        readonly int _buildPrice, _productPrice;
        readonly Color _accent;
        public Transform Vendor => _vendor.transform;

        public TycoonPlatform(Transform parent, Vector3 ground, string name, int buildPrice,
            int productPrice, Color color)
        {
            _name = name;
            _buildPrice = buildPrice;
            _productPrice = productPrice;
            _accent = Color.Lerp(color, RetroWorldArt.Cream, .3f);
            var root = new GameObject("Plataforma_" + name);
            root.transform.SetParent(parent, false);
            root.transform.position = ground;

            var pad = CreateSprite(root.transform, "PlataformaConstruccion", RetroWorldArt.Pad,
                new Vector3(0, .035f, 0), new Vector3(2.5f, 2.5f, 1));
            pad.transform.localRotation = Quaternion.Euler(90, 0, 0);
            _corners = CreateSprite(root.transform, "MarcasDeEstado", RetroWorldArt.Corners,
                new Vector3(0, .045f, 0), new Vector3(2.5f, 2.5f, 1));
            _corners.transform.localRotation = Quaternion.Euler(90, 0, 0);

            _construction = new GameObject("CartelConstruccion");
            _construction.transform.SetParent(root.transform, false);
            _construction.transform.localPosition = Vector3.up * .65f;
            _construction.AddComponent<BillboardSprite>();
            CreateSprite(_construction.transform, "Madera", RetroWorldArt.Construction,
                Vector3.zero, Vector3.one * 1.3f);
            string marker = Marker(name);
            _symbol = CreateText(_construction.transform, "Icono", marker,
                new Vector3(0, .12f, -.015f), new Vector2(.7f, .6f), marker.Length > 1 ? 3.5f : 5f);
            _symbol.textWrappingMode = TextWrappingModes.NoWrap;
            _symbol.color = RetroWorldArt.Cream;

            var sign = new GameObject("EstructuraYCoste");
            sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = Vector3.up * 2.95f;
            sign.AddComponent<BillboardSprite>();
            CreateSprite(sign.transform, "Marco", RetroWorldArt.Sign, Vector3.zero,
                new Vector3(2.05f, 1.7f, 1));
            _label = CreateText(sign.transform, "Texto", "", new Vector3(0, 0, -.02f),
                new Vector2(3.8f, 1.1f), 2.2f);
            _label.color = RetroWorldArt.Cream;

            _vendor = new GameObject("Vendedor_" + name);
            _vendor.transform.SetParent(root.transform, false);
            _vendor.transform.localPosition = Vector3.up * 1.25f;
            _presentation = root.AddComponent<StationPresentation>();
            _presentation.Initialize(sign);
            SetBuilt(false);
        }

        static string Marker(string name)
        {
            if (name.Contains("RÁFAGA")) return "III";
            if (name.Contains("PERFORANTE") || name.Contains("SPRINT")) return ">>";
            if (name.Contains("ABANICO")) return "V";
            if (name.Contains("FUERZA")) return "F";
            if (name.Contains("ESCUDO")) return "E";
            return name.StartsWith("ARSENAL") ? ">" : "+";
        }

        static SpriteRenderer CreateSprite(Transform parent, string name, Sprite sprite,
            Vector3 position, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return renderer;
        }

        static TextMeshPro CreateText(Transform parent, string name, string content,
            Vector3 position, Vector2 size, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var text = go.AddComponent<TextMeshPro>();
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = size;
            text.GetComponent<MeshRenderer>().sortingOrder = 14;
            return text;
        }

        public void SetBuilt(bool built)
        {
            _vendor.SetActive(built);
            _construction.SetActive(!built);
            _corners.color = built ? _accent : Color.Lerp(_accent, RetroWorldArt.Ink, .45f);
            _label.text = _name + (built ? "\nCOMPRAR · " + _productPrice :
                "\nCONSTRUIR · " + _buildPrice) + " PT" +
                (built ? "" : "\nPRODUCTO · " + _productPrice + " PT");
        }

        public void SetPurchased()
        {
            _corners.color = new Color32(134, 185, 107, 255);
            _label.text = _name + "\nADQUIRIDA";
        }
    }
}
