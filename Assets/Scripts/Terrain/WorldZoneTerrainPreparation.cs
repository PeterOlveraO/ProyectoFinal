using UnityEngine;

namespace Game.Terrain
{
    /// <summary>Deterministic access corridors, gate clearings and permanent mountain mask.</summary>
    public static class WorldZoneTerrainPreparation
    {
        public static void Apply(HeightMap map, TerrainSettings settings)
        {
            var layout = new WorldZoneLayout(map.Width, map.Depth, settings.blockSize, Vector3.zero);
            // Four cardinal routes connect both biome cliffs. A four-unit-wide road gives
            // the player and enemy footprints room to turn on the steps.
            int halfWidth = Mathf.Max(1, Mathf.CeilToInt(2f / settings.blockSize));
            int cx = map.Width / 2, cz = map.Depth / 2;
            for (int direction = 0; direction < 4; direction++)
            {
                int sx = direction == 0 ? 1 : direction == 1 ? -1 : 0;
                int sz = direction == 2 ? 1 : direction == 3 ? -1 : 0;
                int previous = map.Get(cx, cz);
                for (int distance = 0; distance < Mathf.Max(map.Width, map.Depth); distance++)
                {
                    int x = cx + sx * distance, z = cz + sz * distance;
                    if (!map.Contains(x, z) || layout.IsMountain(Centre(x, z, settings.blockSize))) break;
                    int level = Mathf.Clamp(map.Get(x, z), previous - 1, previous + 1);
                    for (int side = -halfWidth; side < halfWidth; side++)
                    {
                        int nx = x + (sz != 0 ? side : 0), nz = z + (sx != 0 ? side : 0);
                        if (map.Contains(nx, nz) && !map.IsLakeCell(nx, nz)) map.Set(nx, nz, level);
                    }
                    previous = level;
                }
            }

            // The outer rectangle never crosses the concentric biome cliffs. It ties
            // the corner stations to both adjacent side regions with a four-unit road.
            int west = Mathf.FloorToInt(map.Width * 5f / 32f);
            int east = Mathf.FloorToInt(map.Width * 27f / 32f);
            int south = Mathf.FloorToInt(map.Depth * 5f / 32f);
            int north = Mathf.FloorToInt(map.Depth * 27f / 32f);
            CarveRoad(west, south, east, south);
            CarveRoad(west, north, east, north);
            CarveRoad(west, south, west, north);
            CarveRoad(east, south, east, north);

            // Anchor offsets are deliberately cell units, matching WorldShopPlacement.
            // A shared level at each regional centre makes the entire straight approach
            // to each vendor walkable, not just its isolated 3x3 footprint.
            float fixtureScale = Mathf.Min(1f, Mathf.Min(map.Width, map.Depth) / 256f);
            for (int zone = 1; zone <= 9; zone++)
            {
                Rect bounds = layout.Bounds(zone);
                int zx = Mathf.Clamp(Mathf.FloorToInt(bounds.center.x / settings.blockSize), 0, map.Width - 1);
                int zz = Mathf.Clamp(Mathf.FloorToInt(bounds.center.y / settings.blockSize), 0, map.Depth - 1);
                int level = map.Get(zx, zz);
                int pad = Mathf.Max(1, Mathf.CeilToInt(3f * fixtureScale / settings.blockSize));
                int blend = Mathf.Max(2, Mathf.CeilToInt(4f * fixtureScale / settings.blockSize));
                for (int slot = 0; slot < WorldZoneLayout.VendorCount(zone); slot++)
                {
                    Vector2Int raw = WorldZoneLayout.VendorOffset(zone, slot);
                    int ox = Mathf.RoundToInt(raw.x * fixtureScale), oz = Mathf.RoundToInt(raw.y * fixtureScale);
                    // Rectangle includes the centre-to-vendor access, avoiding a private
                    // "island" plateau connected only by an accidental diagonal step.
                    Flatten(Mathf.Min(zx, zx + ox) - pad, Mathf.Min(zz, zz + oz) - pad,
                        Mathf.Max(zx, zx + ox) + pad, Mathf.Max(zz, zz + oz) + pad, blend, level);
                }
            }

            // All twelve shared edges get a dry, broad station/gate approach. Purchase
            // regions and shop generation can use these without modifying biome IDs.
            for (int a = 1; a <= 9; a++)
            for (int b = a + 1; b <= 9; b++)
            {
                if (!layout.TryGetGate(a, b, out Vector3 gate, out _)) continue;
                int gx = Mathf.Clamp(Mathf.FloorToInt(gate.x / settings.blockSize), 0, map.Width - 1);
                int gz = Mathf.Clamp(Mathf.FloorToInt(gate.z / settings.blockSize), 0, map.Depth - 1);
                int level = map.Get(gx, gz);
                int pad = Mathf.Max(1, Mathf.CeilToInt(4f / settings.blockSize));
                int blend = Mathf.Max(2, Mathf.CeilToInt(4f / settings.blockSize));
                Flatten(gx - pad, gz - pad, gx + pad - 1, gz + pad - 1, blend, level);
            }

            for (int z = 0; z < map.Depth; z++)
            for (int x = 0; x < map.Width; x++)
            {
                Vector3 position = Centre(x, z, settings.blockSize);
                if (!layout.IsMountain(position)) continue;
                map.SetMountainCell(x, z, true);
                // Coarse, faceted peaks with a sheer inner face (never a climbable .5 staircase).
                float noise = Mathf.PerlinNoise(x * .095f + settings.seed * .003f,
                    z * .095f + settings.seed * .007f);
                float height = Mathf.Lerp(8f, 14f, Mathf.Clamp01((noise - .25f) * 2f));
                map.Set(x, z, Mathf.RoundToInt(height / settings.stepHeight));
            }

            void Flatten(int minX, int minZ, int maxX, int maxZ, int blend, int level)
            {
                for (int z = minZ - blend; z <= maxZ + blend; z++)
                for (int x = minX - blend; x <= maxX + blend; x++)
                {
                    if (!map.Contains(x, z) || map.IsLakeCell(x, z) ||
                        layout.IsMountain(Centre(x, z, settings.blockSize))) continue;
                    int allowed = Mathf.Max(0, Mathf.Max(Mathf.Max(minX - x, x - maxX),
                        Mathf.Max(minZ - z, z - maxZ)));
                    map.Set(x, z, Mathf.Clamp(map.Get(x, z), Mathf.Max(0, level - allowed), level + allowed));
                }
            }

            void CarveRoad(int x0, int z0, int x1, int z1)
            {
                int sx = x1 == x0 ? 0 : 1, sz = z1 == z0 ? 0 : 1;
                int length = Mathf.Max(x1 - x0, z1 - z0);
                int previous = map.Get(x0, z0);
                for (int i = 0; i <= length; i++)
                {
                    int x = x0 + sx * i, z = z0 + sz * i;
                    int level = Mathf.Clamp(map.Get(x, z), previous - 1, previous + 1);
                    for (int side = -halfWidth; side < halfWidth; side++)
                    {
                        int nx = x + (sz != 0 ? side : 0), nz = z + (sx != 0 ? side : 0);
                        if (map.Contains(nx, nz) && !map.IsLakeCell(nx, nz) &&
                            !layout.IsMountain(Centre(nx, nz, settings.blockSize))) map.Set(nx, nz, level);
                    }
                    previous = level;
                }
            }
        }

        static Vector3 Centre(int x, int z, float block) => new Vector3((x + .5f) * block, 0, (z + .5f) * block);
    }
}
