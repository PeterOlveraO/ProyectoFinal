using System.Collections.Generic;
using UnityEngine;

namespace Game.Terrain
{
    /// <summary>
    /// Shared XZ collision and four-neighbour flow field. One preallocated BFS per target
    /// cell/access revision, shared by every enemy and spawn query. Never uses Physics or chunks.
    /// </summary>
    public sealed class WorldTraversal
    {
        readonly HeightMap _map;
        readonly float _block, _step;
        readonly Vector3 _origin;
        readonly byte[] _links;
        readonly int[] _distance, _queue;
        readonly byte[] _zoneIds;
        readonly ushort[] _requiredZones;
        readonly bool[] _blocked;
        readonly List<Rect>[] _obstacles;
        int _obstacleRevision, _fieldObstacleRevision = -1;
        IWorldZoneAccess _access;
        int _targetCell = -1, _fieldRevision = -1;
        Vector3 _target;
        public WorldZoneLayout Layout { get; }
        public const float NavigationRadius = .4f;
        public const float NavigationStep = .55f;
        public int FieldBuildCount { get; private set; }
        public int ReachableCount { get; private set; }
        public IWorldZoneAccess ZoneAccess => _access;
        public int UnlockedMask => _access != null ? _access.UnlockedMask : 1;

        public WorldTraversal(HeightMap map, TerrainSettings settings, Vector3 origin,
            IWorldZoneAccess access = null)
        {
            _map = map;
            _block = settings.blockSize;
            _step = settings.stepHeight;
            _origin = origin;
            _access = access;
            Layout = new WorldZoneLayout(map.Width, map.Depth, _block, origin);
            int count = map.Width * map.Depth;
            _links = new byte[count];
            _distance = new int[count];
            _queue = new int[count];
            _zoneIds = new byte[count];
            _requiredZones = new ushort[count];
            _blocked = new bool[count];
            _obstacles = new List<Rect>[count];
            for (int i = 0; i < count; i++)
            {
                int x = i % map.Width, z = i / map.Width;
                _distance[i] = -1;
                _zoneIds[i] = (byte)Layout.ZoneAt(CellCenter(x, z));
                Vector3 centre = CellCenter(x, z);
                for (int id = 1; id <= 9; id++)
                {
                    Rect zone = Layout.Bounds(id);
                    if (centre.x + NavigationRadius >= zone.xMin && centre.x - NavigationRadius <= zone.xMax &&
                        centre.z + NavigationRadius >= zone.yMin && centre.z - NavigationRadius <= zone.yMax)
                        _requiredZones[i] |= (ushort)(1 << (id - 1));
                }
                _blocked[i] = map.IsLakeCell(x, z) || map.IsMountainCell(x, z) ||
                    Layout.IsMountain(CellCenter(x, z));
            }
            RebuildLinks();
        }

        /// <summary>Static world obstacles only. Call once after decoration or when geometry changes.</summary>
        public void RebuildObstacles(IReadOnlyList<Bounds> bounds)
        {
            for (int i = 0; i < _obstacles.Length; i++) _obstacles[i]?.Clear();
            if (bounds != null)
                for (int i = 0; i < bounds.Count; i++)
                {
                    Bounds b = bounds[i];
                    Rect rect = Rect.MinMaxRect(b.min.x - _origin.x, b.min.z - _origin.z,
                        b.max.x - _origin.x, b.max.z - _origin.z);
                    int minX = Mathf.Max(0, Mathf.FloorToInt(rect.xMin / _block));
                    int maxX = Mathf.Min(_map.Width - 1, Mathf.FloorToInt(rect.xMax / _block));
                    int minZ = Mathf.Max(0, Mathf.FloorToInt(rect.yMin / _block));
                    int maxZ = Mathf.Min(_map.Depth - 1, Mathf.FloorToInt(rect.yMax / _block));
                    for (int z = minZ; z <= maxZ; z++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        int cell = z * _map.Width + x;
                        if (_obstacles[cell] == null) _obstacles[cell] = new List<Rect>(2);
                        _obstacles[cell].Add(rect);
                    }
                }
            _obstacleRevision++;
            RebuildLinks();
        }

        public void Invalidate() => _fieldRevision = -1;

        void RebuildLinks()
        {
            for (int z = 0; z < _map.Depth; z++)
            for (int x = 0; x < _map.Width; x++)
            {
                int i = z * _map.Width + x;
                _links[i] = 0;
                if (_blocked[i]) continue;
                for (int d = 0; d < 4; d++)
                {
                    Neighbor(x, z, d, out int nx, out int nz);
                    if (!_map.Contains(nx, nz)) continue;
                    int next = nz * _map.Width + nx;
                    if (!_blocked[next] &&
                        Mathf.Abs(_map.Get(x, z) - _map.Get(nx, nz)) * _step <= NavigationStep + .0001f &&
                        ClearObstacles(CellCenter(x, z), CellCenter(nx, nz), NavigationRadius) &&
                        ClearSegment(CellCenter(x, z), CellCenter(nx, nz), NavigationRadius, true, false) &&
                        ClearCliffEdges(CellCenter(x, z), CellCenter(nx, nz), NavigationRadius, NavigationStep))
                        _links[i] |= (byte)(1 << d);
                }
            }
        }

        public void SetZoneAccess(IWorldZoneAccess access)
        {
            if (ReferenceEquals(_access, access)) return;
            _access = access;
            _fieldRevision = -1;
        }

        public bool IsUnlocked(int zoneId) => zoneId >= 1 && zoneId <= 9 &&
            (UnlockedMask & (1 << (zoneId - 1))) != 0;
        public int ZoneAt(Vector3 position) => Layout.ZoneAt(position);

        public bool CanOccupy(Vector3 position, float radius = 0f) =>
            ClearSegment(position, position, radius, true, true) && ClearObstacles(position, position, radius) &&
            ClearCliffEdges(position, position, radius, NavigationStep);

        public bool CanTraverse(Vector3 from, Vector3 to, float radius = 0f,
            float maxStep = NavigationStep)
        {
            if (float.IsNaN(maxStep) || maxStep < 0f ||
                !ClearSegment(from, to, radius, true, true) || !ClearObstacles(from, to, radius)) return false;
            return CheckHeightTransitions(from, to, maxStep) && ClearCliffEdges(from, to, radius, maxStep);
        }

        /// <summary>Only bounds, mountains and closed zones; Y and terrain steps never stop shots.</summary>
        public bool CanProjectileTraverse(Vector3 from, Vector3 to, float radius = 0f) =>
            ClearSegment(from, to, radius, false, true);

        /// <summary>Placement before a zone is bought: geographic validation without access gating.</summary>
        public bool CanOccupyGeography(Vector3 position, float radius = 0f) =>
            ClearSegment(position, position, radius, true, false);

        public void UpdateTarget(Vector3 position)
        {
            int cell = CellIndex(position), revision = _access != null ? _access.Revision : 0;
            _target = position;
            if (cell == _targetCell && revision == _fieldRevision && _fieldObstacleRevision == _obstacleRevision) return;
            _targetCell = cell;
            _fieldRevision = revision;
            _fieldObstacleRevision = _obstacleRevision;
            FieldBuildCount++;
            ReachableCount = 0;
            for (int i = 0; i < _distance.Length; i++) _distance[i] = -1;
            if (cell < 0 || !CellAllowed(cell) ||
                !CanOccupy(CellCenter(cell % _map.Width, cell / _map.Width), NavigationRadius)) return;
            int head = 0, tail = 0;
            _queue[tail++] = cell;
            _distance[cell] = 0;
            while (head < tail)
            {
                int current = _queue[head++], x = current % _map.Width, z = current / _map.Width;
                int links = _links[current];
                for (int d = 0; d < 4; d++)
                {
                    if ((links & (1 << d)) == 0) continue;
                    Neighbor(x, z, d, out int nx, out int nz);
                    int next = nz * _map.Width + nx;
                    if (_distance[next] >= 0 || !CellAllowed(next)) continue;
                    _distance[next] = _distance[current] + 1;
                    _queue[tail++] = next;
                }
            }
            ReachableCount = tail;
        }

        public bool IsReachable(Vector3 position, float radius = NavigationRadius)
        {
            int index = CellIndex(position);
            return FieldIsCurrent && index >= 0 && _distance[index] >= 0 &&
                CanTraverse(position, CellCenter(index % _map.Width, index / _map.Width), radius);
        }

        public bool TryGetReachableSpawn(Vector3 candidate, out Vector3 ground, float radius = NavigationRadius)
        {
            ground = default;
            if (!IsReachable(candidate, radius) || !CanOccupy(candidate, radius)) return false;
            int cell = CellIndex(candidate);
            ground = new Vector3(candidate.x, _origin.y + _map.Get(cell % _map.Width, cell / _map.Width) * _step, candidate.z);
            return true;
        }

        public Vector3 DirectionToTarget(Vector3 position)
        {
            if (!TryGetNextWaypoint(position, out Vector3 waypoint)) return Vector3.zero;
            Vector3 direction = waypoint - position;
            direction.y = 0f;
            return direction.sqrMagnitude > .000001f ? direction.normalized : Vector3.zero;
        }

        public bool TryGetNextWaypoint(Vector3 position, out Vector3 waypoint)
        {
            waypoint = default;
            int current = CellIndex(position);
            if (!FieldIsCurrent || current < 0 || _distance[current] < 0) return false;
            int x = current % _map.Width, z = current / _map.Width;
            if (current == _targetCell && CanTraverse(position, _target, NavigationRadius))
            {
                waypoint = _target;
                return true;
            }
            int best = _distance[current];
            for (int d = 0; d < 4; d++)
            {
                if ((_links[current] & (1 << d)) == 0) continue;
                Neighbor(x, z, d, out int nx, out int nz);
                int next = nz * _map.Width + nx;
                if (_distance[next] < 0 || _distance[next] >= best) continue;
                Vector3 candidate = CellCenter(nx, nz);
                if (!CanTraverse(position, candidate, NavigationRadius)) continue;
                best = _distance[next];
                waypoint = candidate;
            }
            if (best < _distance[current]) return true;
            // Approach the centre before turning: do not diagonally clip a lake/cliff corner.
            Vector3 centre = CellCenter(x, z);
            if (!CanTraverse(position, centre, NavigationRadius) ||
                (new Vector2(centre.x - position.x, centre.z - position.z)).sqrMagnitude < .0001f) return false;
            waypoint = centre;
            return true;
        }

        bool FieldIsCurrent => _targetCell >= 0 && _fieldRevision == (_access != null ? _access.Revision : 0) &&
            _fieldObstacleRevision == _obstacleRevision;
        bool CellAllowed(int index) => !_blocked[index] && IsUnlocked(_zoneIds[index]) &&
            (_requiredZones[index] & UnlockedMask) == _requiredZones[index];
        int CellIndex(Vector3 world)
        {
            float x = (world.x - _origin.x) / _block, z = (world.z - _origin.z) / _block;
            if (!WorldZoneLayout.Finite(x) || !WorldZoneLayout.Finite(z) ||
                x < 0 || z < 0 || x >= _map.Width || z >= _map.Depth) return -1;
            return Mathf.FloorToInt(z) * _map.Width + Mathf.FloorToInt(x);
        }
        Vector3 CellCenter(int x, int z) => _origin +
            new Vector3((x + .5f) * _block, _map.Get(x, z) * _step, (z + .5f) * _block);

        bool ClearObstacles(Vector3 from, Vector3 to, float radius)
        {
            float ax = from.x - _origin.x, az = from.z - _origin.z;
            float bx = to.x - _origin.x, bz = to.z - _origin.z;
            int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(ax, bx) - radius) / _block));
            int maxX = Mathf.Min(_map.Width - 1, Mathf.FloorToInt((Mathf.Max(ax, bx) + radius) / _block));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(az, bz) - radius) / _block));
            int maxZ = Mathf.Min(_map.Depth - 1, Mathf.FloorToInt((Mathf.Max(az, bz) + radius) / _block));
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                List<Rect> obstacles = _obstacles[z * _map.Width + x];
                if (obstacles == null) continue;
                for (int i = 0; i < obstacles.Count; i++)
                {
                    Rect rect = obstacles[i];
                    if (SegmentHitsRect(ax, az, bx, bz, rect.xMin - radius, rect.yMin - radius,
                        rect.xMax + radius, rect.yMax + radius)) return false;
                }
            }
            return true;
        }

        bool ClearSegment(Vector3 from, Vector3 to, float radius, bool water, bool access)
        {
            if (!WorldZoneLayout.Finite(radius) || radius < 0 ||
                !WorldZoneLayout.Finite(from.x) || !WorldZoneLayout.Finite(from.z) ||
                !WorldZoneLayout.Finite(to.x) || !WorldZoneLayout.Finite(to.z)) return false;
            float ax = from.x - _origin.x, az = from.z - _origin.z;
            float bx = to.x - _origin.x, bz = to.z - _origin.z;
            float border = Layout.MountainWidth;
            if (Mathf.Min(ax, bx) - radius < border || Mathf.Min(az, bz) - radius < border ||
                Mathf.Max(ax, bx) + radius >= Layout.Width - border ||
                Mathf.Max(az, bz) + radius >= Layout.Depth - border) return false;
            // Exact zone rectangles handle scaled fixtures whose splits fall inside a cell.
            if (access)
                for (int id = 1; id <= 9; id++)
                {
                    if (IsUnlocked(id)) continue;
                    Rect r = Layout.Bounds(id);
                    if (SegmentHitsRect(ax, az, bx, bz, r.xMin - _origin.x - radius,
                        r.yMin - _origin.z - radius, r.xMax - _origin.x + radius,
                        r.yMax - _origin.z + radius)) return false;
                }
            int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(ax, bx) - radius) / _block));
            int maxX = Mathf.Min(_map.Width - 1, Mathf.FloorToInt((Mathf.Max(ax, bx) + radius) / _block));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(az, bz) - radius) / _block));
            int maxZ = Mathf.Min(_map.Depth - 1, Mathf.FloorToInt((Mathf.Max(az, bz) + radius) / _block));
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                if (!_map.IsMountainCell(x, z) && (!water || !_map.IsLakeCell(x, z))) continue;
                if (SegmentHitsRect(ax, az, bx, bz, x * _block - radius, z * _block - radius,
                    (x + 1) * _block + radius, (z + 1) * _block + radius)) return false;
            }
            return true;
        }

        // Swept conservative disk (AABB expansion) rather than endpoint-only sampling.
        // Closed intervals deliberately reject a segment exactly grazing an obstacle corner.
        static bool SegmentHitsRect(float ax, float az, float bx, float bz,
            float minX, float minZ, float maxX, float maxZ)
        {
            float lo = 0f, hi = 1f;
            return Slab(ax, bx - ax, minX, maxX, ref lo, ref hi) &&
                Slab(az, bz - az, minZ, maxZ, ref lo, ref hi);
        }
        static bool Slab(float origin, float direction, float min, float max, ref float lo, ref float hi)
        {
            if (Mathf.Abs(direction) < .0000001f) return origin >= min && origin <= max;
            float a = (min - origin) / direction, b = (max - origin) / direction;
            if (a > b) { float swap = a; a = b; b = swap; }
            lo = Mathf.Max(lo, a);
            hi = Mathf.Min(hi, b);
            return lo <= hi;
        }

        bool CheckHeightTransitions(Vector3 from, Vector3 to, float maxStep)
        {
            float ax = (from.x - _origin.x) / _block, az = (from.z - _origin.z) / _block;
            float bx = (to.x - _origin.x) / _block, bz = (to.z - _origin.z) / _block;
            int x = Mathf.FloorToInt(ax), z = Mathf.FloorToInt(az);
            int endX = Mathf.FloorToInt(bx), endZ = Mathf.FloorToInt(bz);
            int sx = bx > ax ? 1 : bx < ax ? -1 : 0, sz = bz > az ? 1 : bz < az ? -1 : 0;
            float dx = sx == 0 ? float.PositiveInfinity : Mathf.Abs(1f / (bx - ax));
            float dz = sz == 0 ? float.PositiveInfinity : Mathf.Abs(1f / (bz - az));
            float tx = sx == 0 ? float.PositiveInfinity : (sx > 0 ? x + 1 - ax : ax - x) * dx;
            float tz = sz == 0 ? float.PositiveInfinity : (sz > 0 ? z + 1 - az : az - z) * dz;
            int budget = _map.Width + _map.Depth + 2;
            while ((x != endX || z != endZ) && budget-- > 0)
            {
                if (Mathf.Abs(tx - tz) < .000001f)
                {
                    // Both cardinal routes must work, never cut diagonally between cliffs.
                    if (!StepAllowed(x, z, x + sx, z, maxStep) ||
                        !StepAllowed(x, z, x, z + sz, maxStep) ||
                        !StepAllowed(x + sx, z, x + sx, z + sz, maxStep) ||
                        !StepAllowed(x, z + sz, x + sx, z + sz, maxStep)) return false;
                    x += sx; z += sz; tx += dx; tz += dz;
                }
                else if (tx < tz)
                {
                    if (!StepAllowed(x, z, x + sx, z, maxStep)) return false;
                    x += sx; tx += dx;
                }
                else
                {
                    if (!StepAllowed(x, z, x, z + sz, maxStep)) return false;
                    z += sz; tz += dz;
                }
            }
            return x == endX && z == endZ;
        }
        bool ClearCliffEdges(Vector3 from, Vector3 to, float radius, float maxStep)
        {
            if (radius <= 0f || float.IsPositiveInfinity(maxStep)) return true;
            float ax = from.x - _origin.x, az = from.z - _origin.z;
            float bx = to.x - _origin.x, bz = to.z - _origin.z;
            int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(ax, bx) - radius) / _block));
            int maxX = Mathf.Min(_map.Width - 1, Mathf.FloorToInt((Mathf.Max(ax, bx) + radius) / _block));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(az, bz) - radius) / _block));
            int maxZ = Mathf.Min(_map.Depth - 1, Mathf.FloorToInt((Mathf.Max(az, bz) + radius) / _block));
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                if (x + 1 < _map.Width &&
                    Mathf.Abs(_map.Get(x, z) - _map.Get(x + 1, z)) * _step > maxStep + .0001f &&
                    SegmentHitsRect(ax, az, bx, bz, (x + 1) * _block - radius, z * _block - radius,
                        (x + 1) * _block + radius, (z + 1) * _block + radius)) return false;
                if (z + 1 < _map.Depth &&
                    Mathf.Abs(_map.Get(x, z) - _map.Get(x, z + 1)) * _step > maxStep + .0001f &&
                    SegmentHitsRect(ax, az, bx, bz, x * _block - radius, (z + 1) * _block - radius,
                        (x + 1) * _block + radius, (z + 1) * _block + radius)) return false;
            }
            return true;
        }
        bool StepAllowed(int x, int z, int nx, int nz, float maxStep) =>
            _map.Contains(x, z) && _map.Contains(nx, nz) &&
            Mathf.Abs(_map.Get(x, z) - _map.Get(nx, nz)) * _step <= maxStep + .0001f;
        static void Neighbor(int x, int z, int direction, out int nx, out int nz)
        {
            nx = x + (direction == 0 ? -1 : direction == 1 ? 1 : 0);
            nz = z + (direction == 2 ? -1 : direction == 3 ? 1 : 0);
        }
    }
}
