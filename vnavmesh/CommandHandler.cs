using System;
using System.Numerics;

namespace Navmesh;

public static class CommandHandler
{
    /// <summary>
    /// Handle chat commands for managing dead zones.
    /// Subcommands:
    ///   add [target] [radius] - Add a dead zone at the player's position, or at target's position if "target" is specified. Radius is optional (default 5).
    ///   remove <index|all> - Remove a dead zone by list index, or remove all dead zones in the current zone.
    ///   list - List all dead zones for the current zone.
    /// </summary>
    public static void HandleCommand(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            Service.ChatGui.Print("Usage: /vnav dead add [target] [radius] | remove <index|all> | list");
            return;
        }

        string[] split = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string subCmd = split[0].ToLower();
        switch (subCmd)
        {
            case "add":
                {
                    bool useTarget = false;
                    float radius = 5.0f;
                    int indexOffset = 1;
                    if (split.Length > 1 && split[1].ToLower() == "target")
                    {
                        useTarget = true;
                        indexOffset = 2;
                    }
                    if (split.Length > indexOffset)
                    {
                        if (!float.TryParse(split[indexOffset], out radius) || radius <= 0)
                        {
                            Service.ChatGui.Print("Invalid radius value. Using default radius 5.");
                            radius = 5.0f;
                        }
                    }
                    Vector3? pos = null;
                    if (useTarget)
                    {
                        var target = Service.TargetManager.Target;
                        if (target == null)
                        {
                            Service.ChatGui.Print("No target selected to add a dead zone at.");
                            return;
                        }
                        pos = target.Position;
                    }
                    else
                    {
                        var player = Service.ClientState.LocalPlayer;
                        if (player == null)
                        {
                            Service.ChatGui.Print("Player not found.");
                            return;
                        }
                        pos = player.Position;
                    }
                    if (pos.HasValue)
                    {
                        ushort zoneId = Service.ClientState.TerritoryType;
                        if (!Service.Config.DeadZones.TryGetValue(zoneId, out var zoneList))
                        {
                            zoneList = new();
                            Service.Config.DeadZones[zoneId] = zoneList;
                        }
                        Vector3 p = pos.Value;
                        zoneList.Add(new DeadZone(p.X, p.Y, p.Z, radius));
                        Service.Config.NotifyModified();
                        Service.ChatGui.Print($"Added dead zone at ({p.X:0.0}, {p.Y:0.0}, {p.Z:0.0}) with radius {radius:0.0} in zone {zoneId}.");
                    }
                    break;
                }
            case "remove":
                {
                    if (split.Length < 2)
                    {
                        Service.ChatGui.Print("Usage: /vnav dead remove <index|all>");
                        return;
                    }
                    string targetArg = split[1].ToLower();
                    ushort zoneId = Service.ClientState.TerritoryType;
                    if (!Service.Config.DeadZones.TryGetValue(zoneId, out var zoneList) || zoneList.Count == 0)
                    {
                        Service.ChatGui.Print("No dead zones to remove for this zone.");
                        return;
                    }
                    if (targetArg == "all")
                    {
                        zoneList.Clear();
                        Service.Config.NotifyModified();
                        Service.ChatGui.Print($"Removed all dead zones in zone {zoneId}.");
                    }
                    else
                    {
                        if (int.TryParse(targetArg, out int removeIndex))
                        {
                            if (removeIndex >= 0 && removeIndex < zoneList.Count)
                            {
                                zoneList.RemoveAt(removeIndex);
                                Service.Config.NotifyModified();
                                Service.ChatGui.Print($"Removed dead zone #{removeIndex} in zone {zoneId}.");
                            }
                            else
                            {
                                Service.ChatGui.Print($"Invalid dead zone index {removeIndex}.");
                            }
                        }
                        else
                        {
                            Service.ChatGui.Print($"Invalid index '{targetArg}'. Please provide a number or 'all'.");
                        }
                    }
                    break;
                }
            case "list":
                {
                    ushort zoneId = Service.ClientState.TerritoryType;
                    if (Service.Config.DeadZones.TryGetValue(zoneId, out var zoneList) && zoneList.Count > 0)
                    {
                        Service.ChatGui.Print($"Dead zones in zone {zoneId}:");
                        for (int i = 0; i < zoneList.Count; i++)
                        {
                            DeadZone dz = zoneList[i];
                            Service.ChatGui.Print($"  {i}: Center=({dz.X:0.0}, {dz.Y:0.0}, {dz.Z:0.0}), Radius={dz.Radius:0.0}");
                        }
                    }
                    else
                    {
                        Service.ChatGui.Print("No dead zones defined for this zone.");
                    }
                    break;
                }
            default:
                {
                    Service.ChatGui.Print("Unknown subcommand. Usage: /vnav dead add [target] [radius] | remove <index|all> | list");
                    break;
                }
        }
    }
}
