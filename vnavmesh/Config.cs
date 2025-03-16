// File: vnavmesh/Config.cs
using System;
using System.Collections.Generic;
using ImGuiNET;

namespace Navmesh
{
    public class Config
    {
        // Existing configuration fields…

        // New flag to toggle drawing dead zones in the world.
        public bool ShowDeadZones = false;

        // Store dead zones by zone ID (or another grouping key).
        public Dictionary<int, List<DeadZone>> DeadZones = new Dictionary<int, List<DeadZone>>();

        /// <summary>
        /// Draws the configuration UI, including the new dead zone visualization toggle.
        /// </summary>
        public void Draw()
        {
            // Draw other configuration settings...
            if (ImGui.Checkbox("Show Dead Zones", ref ShowDeadZones))
            {
                // Save configuration or trigger a change event as needed.
            }
        }
    }
}
