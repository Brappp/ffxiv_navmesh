// File: vnavmesh/DeadZone.cs
using System;
using System.Numerics;
using ImGuiNET;

namespace Navmesh
{
    /// <summary>
    /// Represents a spherical dead zone (an area that blocks movement).
    /// </summary>
    public class DeadZone
    {
        // Position in world coordinates.
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        // The radius of the dead zone.
        public float Radius { get; set; }

        public DeadZone(float x, float y, float z, float radius)
        {
            X = x;
            Y = y;
            Z = z;
            Radius = radius;
        }

        /// <summary>
        /// Draws the ImGui UI for editing this dead zone’s parameters.
        /// Uses sliders for the radius for an interactive resizing experience.
        /// </summary>
        public void DrawUI()
        {
            // Allow editing of the position.
            float x = X, y = Y, z = Z;
            if (ImGui.InputFloat("X", ref x))
            {
                X = x;
            }
            if (ImGui.InputFloat("Y", ref y))
            {
                Y = y;
            }
            if (ImGui.InputFloat("Z", ref z))
            {
                Z = z;
            }

            // Use a slider for the radius. Adjust the min and max values as appropriate.
            float radius = Radius;
            if (ImGui.SliderFloat("Radius", ref radius, 0.1f, 30.0f, "%.2f"))
            {
                Radius = radius;
            }
        }
    }
}
