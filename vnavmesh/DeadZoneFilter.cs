// File: vnavmesh/DeadZoneFilter.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using DotRecast.Detour;
using DotRecast.Core.Numerics;

namespace Navmesh
{
    /// <summary>
    /// A custom query filter that wraps the default filter and rejects any
    /// navmesh polygon whose center lies within one of the dead zones.
    /// </summary>
    public class DeadZoneFilter : IDtQueryFilter
    {
        private IDtQueryFilter baseFilter;
        private List<DeadZone> deadZones;

        public DeadZoneFilter(List<DeadZone> zones)
        {
            // Use the default filter as our base.
            baseFilter = new DtQueryDefaultFilter();
            deadZones = zones;
        }

        /// <summary>
        /// Computes the center of a polygon given its tile and poly.
        /// </summary>
        private Vector3 GetPolyCenter(DtMeshTile tile, DtPoly poly)
        {
            Vector3 center = Vector3.Zero;
            int count = poly.vertCount;
            for (int i = 0; i < count; i++)
            {
                int vi = poly.verts[i];
                // Convert raw vertex coordinates (stored in tile.data.verts as a float[] array) to Vector3.
                Vector3 v = new Vector3(
                    tile.data.verts[vi * 3],
                    tile.data.verts[vi * 3 + 1],
                    tile.data.verts[vi * 3 + 2]
                );
                center += v;
            }
            if (count > 0)
            {
                center /= count;
            }
            return center;
        }

        /// <summary>
        /// Determines whether a given polygon passes the filter (i.e. is not inside a dead zone).
        /// </summary>
        public bool PassFilter(long polyRef, DtMeshTile tile, DtPoly poly)
        {
            if (!baseFilter.PassFilter(polyRef, tile, poly))
            {
                return false;
            }
            Vector3 center = GetPolyCenter(tile, poly);
            foreach (var dz in deadZones)
            {
                Vector3 dzCenter = new Vector3(dz.X, dz.Y, dz.Z);
                if (Vector3.Distance(center, dzCenter) <= dz.Radius)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns the cost for a polygon using the base filter.
        /// </summary>
        public float GetCost(long polyRef, DtMeshTile tile, DtPoly poly)
        {
            return baseFilter.GetCost(polyRef, tile, poly);
        }

        /// <summary>
        /// Public overload for GetCost that takes 8 parameters.
        /// This allows callers that supply only 8 arguments to work.
        /// </summary>
        public float GetCost(RcVec3f startPos, RcVec3f endPos,
                             long startRef, DtMeshTile startTile, DtPoly startPoly,
                             long endRef, DtMeshTile endTile, DtPoly endPoly)
        {
            // Forward to the full 11-parameter version using 0, null, null as defaults.
            return GetCost(startPos, endPos, startRef, startTile, startPoly,
                           endRef, endTile, endPoly, 0, null, null);
        }

        /// <summary>
        /// Explicit interface implementation for GetCost with 11 parameters.
        /// This method signature exactly matches what the interface requires.
        /// </summary>
        float IDtQueryFilter.GetCost(RcVec3f startPos, RcVec3f endPos,
                                     long startRef, DtMeshTile startTile, DtPoly startPoly,
                                     long endRef, DtMeshTile endTile, DtPoly endPoly,
                                     long prevRef, DtMeshTile prevTile, DtPoly prevPoly)
        {
            return GetCost(startPos, endPos, startRef, startTile, startPoly,
                           endRef, endTile, endPoly, prevRef, prevTile, prevPoly);
        }

        /// <summary>
        /// Full implementation of GetCost that takes 11 parameters.
        /// Converts the RcVec3f parameters to System.Numerics.Vector3 and returns the Euclidean distance.
        /// </summary>
        public float GetCost(RcVec3f startPos, RcVec3f endPos,
                             long startRef, DtMeshTile startTile, DtPoly startPoly,
                             long endRef, DtMeshTile endTile, DtPoly endPoly,
                             long prevRef, DtMeshTile prevTile, DtPoly prevPoly)
        {
            // Convert RcVec3f to System.Numerics.Vector3 using the uppercase properties.
            Vector3 s = new Vector3(startPos.X, startPos.Y, startPos.Z);
            Vector3 e = new Vector3(endPos.X, endPos.Y, endPos.Z);
            return Vector3.Distance(s, e);
        }
    }
}
