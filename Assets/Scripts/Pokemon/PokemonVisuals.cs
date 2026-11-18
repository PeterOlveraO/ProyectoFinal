using System.Collections.Generic;
using Game.Rendering;
using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>
    /// Utilidades para colocar sprites de Pokémon como carteles 2D dentro del mundo 3D.
    /// </summary>
    public static class PokemonVisuals
    {
        public enum Facing { Front, Back, Left, Right }

        /// <summary>
        /// Movement is projected onto the horizontal camera axes; standing still keeps the
        /// last direction. The returned direction does not depend on the actor's rotation.
        /// </summary>
        public static Facing Direction(Vector3 movement, Vector3 cameraRight, Vector3 cameraForward, Facing previous)
        {
            movement.y = 0f;
            cameraRight.y = 0f;
            cameraForward.y = 0f;
            if (movement.sqrMagnitude < 0.00000001f ||
                cameraRight.sqrMagnitude < 0.00000001f || cameraForward.sqrMagnitude < 0.00000001f)
                return previous;

            float horizontal = Vector3.Dot(movement, cameraRight.normalized);
            float depth = Vector3.Dot(movement, cameraForward.normalized);
            if (Mathf.Abs(horizontal) > Mathf.Abs(depth))
                return horizontal < 0f ? Facing.Left : Facing.Right;
            return depth > 0f ? Facing.Back : Facing.Front;
        }

        /// <summary>
        /// Frames are isolated from PokemonSprites so they never enter the enemy pool.
        /// A directory named after the assigned source sprite may contain front_0,
        /// front_1, back_0, back_1, side_0 and side_1 sprites. Side faces right.
        /// Missing views retain the assigned sprite instead of substituting another Pokémon.
        /// </summary>
        public sealed class DirectionalAnimation
        {
            const float FrameDuration = 0.16f;
            static readonly Dictionary<Sprite, Sprite[]> Cache = new Dictionary<Sprite, Sprite[]>();
            readonly SpriteRenderer renderer;
            readonly Sprite source;
            readonly Sprite[] frames;
            Facing facing = Facing.Front;
            float elapsed;

            public DirectionalAnimation(SpriteRenderer renderer, Sprite source)
            {
                this.renderer = renderer;
                this.source = source;
                if (!Cache.TryGetValue(source, out frames))
                {
                    frames = new Sprite[6];
                    string path = "PokemonAnimations/" + source.name + "/";
                    string[] names = { "front_0", "front_1", "back_0", "back_1", "side_0", "side_1" };
                    for (int i = 0; i < frames.Length; i++)
                        frames[i] = Resources.Load<Sprite>(path + names[i]);
                    Cache.Add(source, frames);
                }
            }

            public void Update(Vector3 movement, Camera camera, float deltaTime)
            {
                movement.y = 0f;
                bool moving = movement.sqrMagnitude >= 0.00000001f;
                if (moving && camera != null)
                {
                    Facing next = Direction(movement, camera.transform.right, camera.transform.forward, facing);
                    if (next != facing) elapsed = 0f;
                    else elapsed += Mathf.Max(0f, deltaTime);
                    facing = next;
                }
                else if (!moving) elapsed = 0f;

                int view = facing == Facing.Front ? 0 : facing == Facing.Back ? 2 : 4;
                int frame = moving ? (int)(elapsed / FrameDuration) % 2 : 0;
                renderer.sprite = frames[view + frame] ?? frames[view] ?? source;
                renderer.flipX = facing == Facing.Left;
            }
        }

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
