using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Navmesh.Debug;
using Navmesh.Movement;
using System;
using System.Numerics;

namespace Navmesh;

public class MainWindow : Window, IDisposable
{
    private FollowPath _path;
    private DebugDrawer _dd = new();
    private DebugGameCollision _debugGameColl;
    private DebugNavmeshManager _debugNavmeshManager;
    private DebugNavmeshCustom _debugNavmeshCustom;
    private DebugLayout _debugLayout;

    public MainWindow(NavmeshManager manager, FollowPath path, AsyncMoveRequest move, DTRProvider dtr) : base("Navmesh")
    {
        _path = path;
        _debugGameColl = new(_dd);
        _debugNavmeshManager = new(_dd, _debugGameColl, manager, path, move, dtr);
        _debugNavmeshCustom = new(_dd, _debugGameColl);
        _debugLayout = new(_debugGameColl);
    }

    public void Dispose()
    {
        _debugLayout.Dispose();
        _debugNavmeshCustom.Dispose();
        _debugNavmeshManager.Dispose();
        _debugGameColl.Dispose();
        _dd.Dispose();
    }

    public void StartFrame()
    {
        _dd.StartFrame();
    }

    public void EndFrame()
    {
        _debugGameColl.DrawVisualizers();
        if (Service.Config.ShowWaypoints)
        {
            var player = Service.ClientState.LocalPlayer;
            if (player != null)
            {
                var from = player.Position;
                uint color = 0xff00ff00;
                foreach (var to in _path.Waypoints)
                {
                    _dd.DrawWorldLine(from, to, color);
                    _dd.DrawWorldPointFilled(to, 3, 0xff0000ff);
                    from = to;
                    color = 0xff00ffff;
                }
            }
        }
        // Visualize dead zones for the current zone
        var zoneId = Service.ClientState.TerritoryType;
        if (Service.Config.DeadZones.TryGetValue(zoneId, out var deadZoneList))
        {
            _dd.DrawDeadZones(deadZoneList);
        }
        _dd.EndFrame();
    }

    public override void Draw()
    {
        using var tabs = ImRaii.TabBar("Tabs");
        if (tabs)
        {
            using (var tab = ImRaii.TabItem("Config"))
                if (tab) Service.Config.Draw();
            using (var tab = ImRaii.TabItem("Layout"))
                if (tab) _debugLayout.Draw();
            using (var tab = ImRaii.TabItem("Collision"))
                if (tab) _debugGameColl.Draw();
            using (var tab = ImRaii.TabItem("Navmesh manager"))
                if (tab) _debugNavmeshManager.Draw();
            using (var tab = ImRaii.TabItem("Navmesh custom"))
                if (tab) _debugNavmeshCustom.Draw();
            using (var tab = ImRaii.TabItem("Dead Zones"))
            {
                if (tab) DrawDeadZonesTab();
            }
        }
    }

    private void DrawDeadZonesTab()
    {
        ushort zoneId = Service.ClientState.TerritoryType;
        ImGui.Text($"Current Zone ID: {zoneId}");
        if (!Service.Config.DeadZones.TryGetValue(zoneId, out var zonesList))
        {
            zonesList = new();
            Service.Config.DeadZones[zoneId] = zonesList;
        }

        bool changed = false;
        // Display existing dead zones and allow editing
        for (int i = 0; i < zonesList.Count; i++)
        {
            DeadZone dz = zonesList[i];
            ImGui.PushID(i);
            float x = dz.X, y = dz.Y, z = dz.Z, r = dz.Radius;
            if (ImGui.InputFloat("X", ref x, 0, 0, "%.1f"))
            {
                dz.X = x;
                changed = true;
            }
            if (ImGui.InputFloat("Y", ref y, 0, 0, "%.1f"))
            {
                dz.Y = y;
                changed = true;
            }
            if (ImGui.InputFloat("Z", ref z, 0, 0, "%.1f"))
            {
                dz.Z = z;
                changed = true;
            }
            if (ImGui.InputFloat("Radius", ref r, 0, 0, "%.1f"))
            {
                dz.Radius = r;
                changed = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Remove"))
            {
                zonesList.RemoveAt(i);
                changed = true;
                ImGui.PopID();
                i--;
                continue;
            }
            ImGui.PopID();
        }

        // Buttons to add new dead zones
        if (ImGui.Button("Add Dead Zone (Player)"))
        {
            var player = Service.ClientState.LocalPlayer;
            if (player != null)
            {
                Vector3 pos = player.Position;
                zonesList.Add(new DeadZone(pos.X, pos.Y, pos.Z, 5.0f));
                changed = true;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Add Dead Zone (Target)"))
        {
            var target = Service.TargetManager.Target;
            if (target != null)
            {
                Vector3 pos = target.Position;
                zonesList.Add(new DeadZone(pos.X, pos.Y, pos.Z, 5.0f));
                changed = true;
            }
        }

        if (changed)
        {
            Service.Config.NotifyModified();
        }
    }
}
