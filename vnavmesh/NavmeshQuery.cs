using System;
using System.Collections.Generic;
using System.Numerics;
using DotRecast.Core.Numerics;
using DotRecast.Detour;

namespace Navmesh
{
    public class NavmeshQuery
    {
        private DtNavMeshQuery query;
        public IDtQueryFilter Filter { get; set; }

        public NavmeshQuery(DtNavMesh navMesh)
        {
            // Use the DtNavMeshQuery constructor that takes only the navMesh.
            query = new DtNavMeshQuery(navMesh);
            Filter = new DtQueryDefaultFilter();
        }

        /// <summary>
        /// Finds a path between the given start and end positions.
        /// If 'deadZones' is provided, we use a DeadZoneFilter to reject any polygon
        /// whose center lies inside a dead zone.
        /// </summary>
        public List<long> FindPath(Vector3 start, Vector3 end, List<DeadZone> deadZones)
        {
            // If we have dead zones, use our custom filter that excludes them.
            if (deadZones != null && deadZones.Count > 0)
            {
                Filter = new DeadZoneFilter(deadZones);
            }
            else
            {
                Filter = new DtQueryDefaultFilter();
            }

            // Convert from System.Numerics.Vector3 to RcVec3f.
            RcVec3f startPos = new RcVec3f(start.X, start.Y, start.Z);
            RcVec3f endPos = new RcVec3f(end.X, end.Y, end.Z);

            // Define extents for searching nearest polygons.
            RcVec3f extents = new RcVec3f(0.5f, 0.5f, 0.5f);

            // Find the nearest polygon to the start position.
            long startRef;
            RcVec3f nearestPt;
            bool over;
            query.FindNearestPoly(startPos, extents, Filter, out startRef, out nearestPt, out over);

            // Find the nearest polygon to the end position.
            long endRef;
            RcVec3f nearestPtEnd;
            bool overEnd;
            query.FindNearestPoly(endPos, extents, Filter, out endRef, out nearestPtEnd, out overEnd);

            // If either reference is zero, we have no valid start or end polygon.
            if (startRef == 0 || endRef == 0)
            {
                return new List<long>();
            }

            // Prepare a list to store the resulting path polygon references.
            List<long> path = new List<long>();

            // Call FindPath. It returns a DtStatus bitfield.
            DtStatus status = query.FindPath(
                startRef,
                endRef,
                startPos,
                endPos,
                Filter,
                ref path,
                default(DtFindPathOption)
            );

            // Check if the operation succeeded using the Succeeded() extension method
            bool isSuccess = status.Succeeded();

            if (isSuccess)
            {
                // Path found successfully.
                return path;
            }
            else
            {
                // Path not found.
                return new List<long>();
            }
        }
    }
}