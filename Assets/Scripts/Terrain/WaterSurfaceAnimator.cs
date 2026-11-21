using UnityEngine;

namespace Game.Terrain
{
    /// <summary>Low-cost pixel-stepped scroll shared by the world's lake material.</summary>
    [DisallowMultipleComponent]
    public sealed class WaterSurfaceAnimator : MonoBehaviour
    {
        const float StepSeconds = .24f;
        const float UvStep = 1f / 32f;
        Material _material;
        float _elapsed;
        int _step;

        public void Initialize(Material material)
        {
            _material = material;
        }

        void Update()
        {
            if (_material == null) return;
            _elapsed += Time.deltaTime;
            if (_elapsed < StepSeconds) return;
            _elapsed %= StepSeconds;
            _step = (_step + 1) & 31;
            _material.mainTextureOffset = new Vector2(_step * UvStep, 0f);
        }
    }
}
