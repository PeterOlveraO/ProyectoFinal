namespace Game.Terrain
{
    /// <summary>
    /// Niveles enteros del mundo completo, un valor por bloque (x hacia +X, z hacia +Z).
    /// </summary>
    public sealed class HeightMap
    {
        public int Width { get; }
        public int Depth { get; }

        readonly int[] _levels;

        public HeightMap(int width, int depth)
        {
            Width = width;
            Depth = depth;
            _levels = new int[width * depth];
        }

        public bool Contains(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Depth;

        public int Get(int x, int z) => _levels[z * Width + x];

        public void Set(int x, int z, int level) => _levels[z * Width + x] = level;
    }
}
