using UnityEngine;

namespace Game.Pokemon
{
    /// <summary>
    /// Owns only the billboard child's scale and vertical support. Six animation frames
    /// share one visible-pixel envelope; the retained initial/source-only sprite uses its
    /// own crop. Alpha centers are deliberately not used to shift X between frames.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PokemonBillboardLayout : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        PokemonSpriteMetrics.Family family;
        PokemonSpriteMetrics.Bounds sourceBounds;
        PokemonSpriteMetrics.Bounds currentBounds;
        float targetHeight, maximumWidth, groundOffset;
        int currentFrame = -1;

        public float SupportHeight { get; private set; }

        public void Configure(SpriteRenderer renderer, Sprite sourceSprite, float height, float maxWidth,
                              float footHeight = 0f)
        {
            spriteRenderer = renderer;
            family = PokemonSpriteMetrics.Find(sourceSprite.name);
            sourceBounds = PokemonSpriteMetrics.Source(sourceSprite);
            groundOffset = footHeight;
            targetHeight = Mathf.Max(0.2f, height);
            maximumWidth = Mathf.Max(0.2f, maxWidth);
            spriteRenderer.sprite = sourceSprite;
            spriteRenderer.flipX = false;
            ApplyFrame(sourceSprite, -1);
        }

        public void SetProfile(float height, float maxWidth)
        {
            targetHeight = Mathf.Max(0.2f, height);
            maximumWidth = Mathf.Max(0.2f, maxWidth);
            if (spriteRenderer != null && spriteRenderer.sprite != null)
                ApplyGeometry(spriteRenderer.sprite);
        }

        public void ApplyFrame(Sprite sprite, int frame)
        {
            currentFrame = frame;
            var bounds = frame >= 0 && family != null ? family.frames[frame] : sourceBounds;
            currentBounds = bounds != null && bounds.Matches(sprite)
                ? bounds : PokemonSpriteMetrics.FullRect(sprite);
            ApplyGeometry(sprite);
        }

        void ApplyGeometry(Sprite sprite)
        {
            float visibleHeight = currentBounds.visibleHeight;
            float visibleWidth = currentBounds.visibleWidth;
            if (currentFrame >= 0 && family != null)
            {
                visibleHeight = family.maxVisibleHeight;
                visibleWidth = family.maxVisibleWidth;
            }
            float scale = Mathf.Min(targetHeight * sprite.pixelsPerUnit / visibleHeight,
                                    maximumWidth * sprite.pixelsPerUnit / visibleWidth);
            transform.localScale = new Vector3(scale, scale, 1f);
            SupportHeight = (sprite.pivot.y - currentBounds.bottom) * scale / sprite.pixelsPerUnit;
            Vector3 position = transform.localPosition;
            position.y = groundOffset + SupportHeight;
            transform.localPosition = position;
        }
    }
}
