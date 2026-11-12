using Game.Rendering;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>
    /// Utilidades para colocar sprites de Pokémon como carteles 2D dentro del mundo 3D.
    /// </summary>
    public static class PokemonVisuals
    {
        /// <summary>
        /// Crea un SpriteRenderer billboard. El pivote de los sprites de Pokémon es el centro,
        /// así que <paramref name="height"/> es la altura total en unidades de mundo.
        /// </summary>
        public static SpriteRenderer CreateBillboard(Transform parent, string name, Sprite sprite,
                                                     float height, bool continuous)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;

            float spriteHeight = sprite.rect.height / sprite.pixelsPerUnit;
            float scale = spriteHeight > 0.0001f ? height / spriteHeight : 1f;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var billboard = go.AddComponent<BillboardSprite>();
            billboard.SetContinuous(continuous);
            return renderer;
        }
    }
}
