// File: vnavmesh/MainWindow.cs
using System;
using System.Collections.Generic;
using ImGuiNET;
using System.Numerics;
using Navmesh.Debug;

namespace Navmesh
{
    public class MainWindow
    {
        private Config config;
        private DebugDrawer debugDrawer;
        private int currentZoneId; // For example, the current zone’s identifier.

        public MainWindow(Config cfg, DebugDrawer dd)
        {
            config = cfg;
            debugDrawer = dd;
            currentZoneId = 0; // Initialize as appropriate.
        }

        public void Draw()
        {
            // Draw the overall configuration.
            config.Draw();

            // If there are dead zones for the current zone, draw their UI.
            if (config.DeadZones.TryGetValue(currentZoneId, out var deadZoneList))
            {
                if (ImGui.CollapsingHeader("Dead Zones"))
                {
                    for (int i = 0; i < deadZoneList.Count; i++)
                    {
                        ImGui.PushID(i);
                        if (ImGui.TreeNode($"Dead Zone {i}"))
                        {
                            deadZoneList[i].DrawUI();
                            ImGui.TreePop();
                        }
                        ImGui.PopID();
                    }
                }
            }

            // Draw world debug elements.
            DrawWorld();
        }

        // Draws elements into the world view.
        private void DrawWorld()
        {
            // Other drawing code…

            // Draw dead zones if the config flag is enabled.
            if (config.ShowDeadZones && config.DeadZones.TryGetValue(currentZoneId, out var deadZoneList))
            {
                // Iterate over each dead zone and draw it as a wireframe sphere.
                foreach (var dz in deadZoneList)
                {
                    // debugDrawer.DrawDeadZones expects an IEnumerable of DeadZone.
                    List<DeadZone> list = new List<DeadZone> { dz };
                    debugDrawer.DrawDeadZones(list, 0xFFFF0000, 2);
                }
            }
        }
    }
}
