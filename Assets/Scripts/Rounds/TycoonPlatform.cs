using Game.Rendering;
using TMPro;
using UnityEngine;

namespace Game.Rounds
{
    /// <summary>A walk-through build pad, persistent sign and separately revealed vendor.</summary>
    public sealed class TycoonPlatform
    {
        readonly GameObject _vendor;
        readonly TextMeshPro _label;
        readonly string _name;
        readonly int _buildPrice, _productPrice;
        public Transform Vendor => _vendor.transform;

        public TycoonPlatform(Transform parent, Vector3 ground, string name, int buildPrice,
            int productPrice, Color color)
        {
            _name = name;
            _buildPrice = buildPrice;
            _productPrice = productPrice;
            var root = new GameObject("Plataforma_" + name);
            root.transform.SetParent(parent, false);
            root.transform.position = ground;
            var pad = new GameObject("PlataformaConstruccion");
            pad.transform.SetParent(root.transform, false);
            pad.transform.localPosition = Vector3.up * .035f;
            pad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var padRenderer = pad.AddComponent<SpriteRenderer>();
            padRenderer.sprite = ProceduralSprites.RoundedRect;
            padRenderer.drawMode = SpriteDrawMode.Sliced;
            padRenderer.size = new Vector2(2.5f, 2.5f);
            padRenderer.color = color;
            var sign = new GameObject("EstructuraYCoste");
            sign.transform.SetParent(root.transform, false);
            sign.transform.localPosition = Vector3.up * 3.4f;
            sign.AddComponent<BillboardSprite>().SetContinuous(false);
            var face = new GameObject("Texto");
            face.transform.SetParent(sign.transform, false);
            face.transform.localRotation = Quaternion.Euler(0, 180, 0);
            _label = face.AddComponent<TextMeshPro>();
            _label.fontSize = 2.5f;
            _label.fontStyle = FontStyles.Bold;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;
            _label.rectTransform.sizeDelta = new Vector2(5.5f, 2f);
            _label.GetComponent<MeshRenderer>().sortingOrder = 14;
            _vendor = new GameObject("Vendedor_" + name);
            _vendor.transform.SetParent(root.transform, false);
            _vendor.transform.localPosition = Vector3.up * 1.25f;
            SetBuilt(false);
        }

        public void SetBuilt(bool built)
        {
            _vendor.SetActive(built);
            _label.text = _name + (built ? "\nCOMPRAR · " + _productPrice :
                "\nCONSTRUIR · " + _buildPrice) + " PT" +
                (built ? "" : "\nPRODUCTO · " + _productPrice + " PT");
        }
    }
}
