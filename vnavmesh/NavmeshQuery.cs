using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Recast;
using Navmesh.NavVolume;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Navmesh
{
    /// <summary>
    /// Extension methods to convert between System.Numerics.Vector3 and DotRecast.Core.Numerics.RcVec3f.
    /// </summary>
    public static class VectorExtensions
    {
        /// <summary>
        /// Converts a System.Numerics.Vector3 to a RcVec3f.
        /// </summary>
        public static RcVec3f ToRcVec3f(this Vector3 v)
        {
            return new RcVec3f(v.X, v.Y, v.Z);
        }

        /// <summary>
        /// Converts a RcVec3f to a System.Numerics.Vector3.
        /// </summary>
        public static Vector3 ToSystemVector(this RcVec3f v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }
    }

    /// <summary>
    /// NavmeshQuery encapsulates mesh-based queries using the Detour API.
    /// </summary>
    public class NavmeshQuery
    {
        // Helper class to collect polygon references from a query.
        private class IntersectQuery : IDtPolyQuery
        {
            public List<long> Result = new List<long>();
            public void Process(DtMeshTile tile, DtPoly poly, long refs) => Result.Add(refs);
        }

        public DtNavMeshQuery MeshQuery;
        public VoxelPathfind? VolumeQuery;
        private IDtQueryFilter _filter = new DtQueryDefaultFilter();

        /// <summary>
        /// Constructs a new NavmeshQuery using the provided Navmesh.
        /// The Navmesh type is assumed to expose a Mesh property (of type DtNavMesh)
        /// and optionally a Volume property.
        /// </summary>
        public NavmeshQuery(Navmesh navmesh)
        {
            // Construct the Detour navmesh query using the underlying mesh.
            MeshQuery = new DtNavMeshQuery(navmesh.Mesh);
            if (navmesh.Volume != null)
                VolumeQuery = new VoxelPathfind(navmesh.Volume);
        }

        /// <summary>
        /// Finds a path between two world positions on the navmesh.
        /// </summary>
        public List<Vector3> PathfindMesh(Vector3 from, Vector3 to, bool useRaycast, bool useStringPulling, CancellationToken cancel)
        {
            var startRef = FindNearestMeshPoly(from);
            var endRef = FindNearestMeshPoly(to);
            Service.Log.Debug($"[pathfind] poly {startRef:X} -> {endRef:X}");
            if (startRef == 0 || endRef == 0)
            {
                Service.Log.Error($"Failed to find a path from {from} ({startRef:X}) to {to} ({endRef:X}): failed to find polygon on a mesh");
                return new List<Vector3>();
            }

            var timer = Timer.Create();
            var polysPath = new List<long>();
            // Create extents for the query using RcVec3f.
            var opt = new DtFindPathOption(useRaycast ? DtFindPathOptions.DT_FINDPATH_ANY_ANGLE : 0, float.MaxValue);
            MeshQuery.FindPath(startRef, endRef, from.ToRcVec3f(), to.ToRcVec3f(), _filter, ref polysPath, opt);
            if (polysPath.Count == 0)
            {
                Service.Log.Error($"Failed to find a path from {from} ({startRef:X}) to {to} ({endRef:X}): failed to find path on mesh");
                return new List<Vector3>();
            }
            Service.Log.Debug($"Pathfind took {timer.Value().TotalSeconds:f3}s: {string.Join(", ", polysPath.Select(r => r.ToString("X")))}");

            // In case of partial path, get the last point from the mesh.
            RcVec3f endPos = to.ToRcVec3f();

            if (useStringPulling)
            {
                var straightPath = new List<DtStraightPath>();
                DtStatus success = MeshQuery.FindStraightPath(from.ToRcVec3f(), endPos, polysPath, ref straightPath, 1024, 0);
                if (success.Failed())
                    Service.Log.Error($"Failed to find a path from {from} ({startRef:X}) to {to} ({endRef:X}): failed to find straight path ({success.Value:X})");
                var res = straightPath.Select(p => p.pos.ToSystemVector()).ToList();
                res.Add(endPos.ToSystemVector());
                return res;
            }
            else
            {
                var res = polysPath.Select(r => MeshQuery.GetAttachedNavMesh().GetPolyCenter(r).ToSystemVector()).ToList();
                res.Add(endPos.ToSystemVector());
                return res;
            }
        }

        /// <summary>
        /// Finds a path using volume-based queries.
        /// (Stub implementation – extend if you need volume-based pathfinding.)
        /// </summary>
        public List<Vector3> PathfindVolume(Vector3 from, Vector3 to, bool useRaycast, bool useStringPulling, CancellationToken cancel)
        {
            if (VolumeQuery == null)
            {
                Service.Log.Error("Nav volume was not built");
                return new List<Vector3>();
            }

            var startVoxel = FindNearestVolumeVoxel(from);
            var endVoxel = FindNearestVolumeVoxel(to);
            Service.Log.Debug($"[pathfind] voxel {startVoxel:X} -> {endVoxel:X}");
            if (startVoxel == VoxelMap.InvalidVoxel || endVoxel == VoxelMap.InvalidVoxel)
            {
                Service.Log.Error($"Failed to find a path from {from} ({startVoxel:X}) to {to} ({endVoxel:X}): failed to find empty voxel");
                return new List<Vector3>();
            }

            var timer = Timer.Create();
            var voxelPath = VolumeQuery.FindPath(startVoxel, endVoxel, from, to, useRaycast, false, cancel);
            if (voxelPath.Count == 0)
            {
                Service.Log.Error($"Failed to find a path from {from} ({startVoxel:X}) to {to} ({endVoxel:X}): failed to find path on volume");
                return new List<Vector3>();
            }
            Service.Log.Debug($"Pathfind took {timer.Value().TotalSeconds:f3}s: {string.Join(", ", voxelPath.Select(r => $"{r.p} {r.voxel:X}"))}");
            var res = voxelPath.Select(r => r.p).ToList();
            res.Add(to);
            return res;
        }

        /// <summary>
        /// Returns 0 if not found; otherwise returns the polygon reference for point p.
        /// </summary>
        public long FindNearestMeshPoly(Vector3 p, float halfExtentXZ = 5, float halfExtentY = 5)
        {
            // Create extents as an RcVec3f.
            var extents = new RcVec3f(halfExtentXZ, halfExtentY, halfExtentXZ);
            MeshQuery.FindNearestPoly(p.ToRcVec3f(), extents, _filter, out var nearestRef, out _, out _);
            return nearestRef;
        }

        /// <summary>
        /// Returns all polygons intersecting a box centered at p with given half extents.
        /// </summary>
        public List<long> FindIntersectingMeshPolys(Vector3 p, Vector3 halfExtent)
        {
            IntersectQuery query = new IntersectQuery();
            MeshQuery.QueryPolygons(p.ToRcVec3f(), halfExtent.ToRcVec3f(), _filter, query);
            return query.Result;
        }

        /// <summary>
        /// Returns the nearest point on the specified polygon.
        /// Returns null if the query fails.
        /// </summary>
        public Vector3? FindNearestPointOnMeshPoly(Vector3 p, long poly)
        {
            DtStatus status = MeshQuery.ClosestPointOnPoly(poly, p.ToRcVec3f(), out RcVec3f closest, out _);
            if (status.Succeeded())
                return closest.ToSystemVector();
            return null;
        }

        /// <summary>
        /// Returns the nearest point on the navmesh from p.
        /// </summary>
        public Vector3? FindNearestPointOnMesh(Vector3 p, float halfExtentXZ = 5, float halfExtentY = 5)
        {
            return FindNearestPointOnMeshPoly(p, FindNearestMeshPoly(p, halfExtentXZ, halfExtentY));
        }

        /// <summary>
        /// Finds the point on the mesh (within a box around p) with the highest Y that is still less than or equal to p.Y.
        /// </summary>
        public Vector3? FindPointOnFloor(Vector3 p, float halfExtentXZ = 5)
        {
            IEnumerable<long> polys = FindIntersectingMeshPolys(p, new Vector3(halfExtentXZ, 2048, halfExtentXZ));
            return polys.Select(poly => FindNearestPointOnMeshPoly(p, poly))
                        .Where(pt => pt != null && pt.Value.Y <= p.Y)
                        .MaxBy(pt => pt!.Value.Y);
        }

        /// <summary>
        /// Returns VoxelMap.InvalidVoxel if not found; otherwise returns the voxel index.
        /// </summary>
        public ulong FindNearestVolumeVoxel(Vector3 p, float halfExtentXZ = 5, float halfExtentY = 5)
        {
            return VolumeQuery != null
                ? VoxelSearch.FindNearestEmptyVoxel(VolumeQuery.Volume, p, new Vector3(halfExtentXZ, halfExtentY, halfExtentXZ))
                : VoxelMap.InvalidVoxel;
        }

        /// <summary>
        /// Collects all mesh polygons reachable from the starting polygon using a flood-fill.
        /// </summary>
        public HashSet<long> FindReachableMeshPolys(long starting, Vector3 startPos, float radius)
        {
            HashSet<long> result = new HashSet<long>();
            if (starting == 0)
                return result;

            List<long> queue = new List<long> { starting };
            while (queue.Count > 0)
            {
                var next = queue[^1];
                queue.RemoveAt(queue.Count - 1);

                if (!result.Add(next))
                    continue; // Already visited

                // Retrieve tile and poly info from the navmesh.
                MeshQuery.GetAttachedNavMesh().GetTileAndPolyByRefUnsafe(next, out DtMeshTile nextTile, out DtPoly nextPoly);
                for (int i = nextTile.polyLinks[nextPoly.index]; i != DtNavMesh.DT_NULL_LINK; i = nextTile.links[i].next)
                {
                    long neighbourRef = nextTile.links[i].refs;
                    if (neighbourRef != 0)
                        queue.Add(neighbourRef);
                }
            }
            return result;
        }
    }
}
