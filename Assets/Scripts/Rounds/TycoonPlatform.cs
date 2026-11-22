using Game.Rendering;
using Game.Terrain;
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
        readonly SpriteRenderer _icon;
        readonly GameObject _acquired;
        readonly Sprite _machineIcon, _productIcon;
        readonly SpriteRenderer _corners;
        readonly StationPresentation _presentation;
        readonly string _name;
        readonly string _structure, _product;
        readonly int _buildPrice, _productPrice;
        readonly Color _accent;
        public Transform Vendor => _vendor.transform;
        public void SetZone(int zone) => _presentation.SetZone(zone);
        public void RegisterFog(WorldZoneFog fog)
        {
            if (fog == null) return;
            Transform root = _vendor.transform.parent;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                fog.RegisterProp(renderer, root.position);
        }

        public TycoonPlatform(Transform parent, Vector3 ground, string name, int buildPrice,
            int productPrice, Color color, StationIcon productIcon, bool arsenal = false)
        {
            _name = name;
            _structure = arsenal ? "ARSENAL" : "MÁQUINA";
            _product = arsenal ? "DISPARO" : "PERK";
            _machineIcon = StationIconArt.Get(arsenal ? StationIcon.Arsenal : StationIcon.Machine);
            _productIcon = StationIconArt.Get(productIcon);
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
            CreateSprite(_construction.transform, "MaquinaPorConstruir", _machineIcon,
                new Vector3(0, .15f, -.015f), Vector3.one * .85f).sortingOrder = 13;
            // Product identity remains visible at a distance, including before construction.
            CreateSprite(_construction.transform, "Especialidad", _productIcon,
                new Vector3(-.42f, .52f, -.025f), Vector3.one * .55f).sortingOrder = 14;

            var sign = new GameObject("EstructuraYCoste");
            sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = Vector3.up * 2.95f;
            sign.AddComponent<BillboardSprite>();
            CreateSprite(sign.transform, "Marco", RetroWorldArt.Sign, Vector3.zero,
                new Vector3(2.05f, 1.7f, 1));
            _icon = CreateSprite(sign.transform, "CompraActual", _machineIcon,
                new Vector3(1.45f, 0, -.025f), Vector3.one * 1.15f);
            _icon.sortingOrder = 14;
            _acquired = CreateSprite(sign.transform, "PerkAdquirido", StationIconArt.Get(StationIcon.Acquired),
                new Vector3(1.08f, -.34f, -.035f), Vector3.one * .55f).gameObject;
            _acquired.GetComponent<SpriteRenderer>().sortingOrder = 15;
            _label = CreateText(sign.transform, "Texto", "", new Vector3(-.4f, 0, -.02f),
                new Vector2(2.9f, 1.1f), 2.1f);
            _label.color = RetroWorldArt.Cream;
            _label.textWrappingMode = TextWrappingModes.NoWrap;

            _vendor = new GameObject("Vendedor_" + name);
            _vendor.transform.SetParent(root.transform, false);
            _vendor.transform.localPosition = Vector3.up * 1.25f;
            _presentation = root.AddComponent<StationPresentation>();
            _presentation.Initialize(sign);
            SetBuilt(false);
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
            _icon.sprite = built ? _productIcon : _machineIcon;
            _acquired.SetActive(false);
            _corners.color = built ? _accent : Color.Lerp(_accent, RetroWorldArt.Ink, .45f);
            _label.text = "<color=#E9C46A><size=80%>" + (built ? "2 · " + _product : "1 · " + _structure) +
                "</size></color>\n" + _name + (built ? "\nCOMPRAR · " + _productPrice :
                "\nCONSTRUIR · " + _buildPrice) + " PT";
        }

        public void SetPurchased()
        {
            _corners.color = new Color32(134, 185, 107, 255);
            _acquired.SetActive(true);
            _label.text = "<color=#96D179><size=80%>" + _product + "</size></color>\n" + _name +
                "\n<color=#96D179>ADQUIRIDO</color>";
        }
    }
}
