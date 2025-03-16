using ImGuiNET;
using Dalamud.Interface;
using Navmesh.Render;
using Navmesh;
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision.Math;

namespace Navmesh.Debug;

public unsafe class DebugDrawer : IDisposable
{
    public RenderContext RenderContext { get; init; } = new();
    public RenderTarget? RenderTarget { get; private set; }
    public EffectMesh? EffectMesh { get; init; }

    public Vector3 Origin;
    public Matrix4x4 View;
    public Matrix4x4 Proj;
    public Matrix4x4 ViewProj;
    public Vector4 NearPlane;
    public float CameraAzimuth;
    public float CameraAltitude;
    public Vector2 ViewportSize;

    private List<(Vector2 from, Vector2 to, uint col, int thickness)> _viewportLines = new();
    private List<(Vector2 center, float radius, uint color)> _viewportCircles = new();

    public DebugDrawer()
    {
        try
        {
            EffectMesh = new(RenderContext);
        }
        catch (Exception ex)
        {
            Service.Log.Error($"Failed to set up renderer; some debug visualization will be unavailable: {ex}");
        }
    }

    public void Dispose()
    {
        EffectMesh?.Dispose();
        RenderTarget?.Dispose();
        RenderContext.Dispose();
    }

    public void StartFrame()
    {
        var controlCamera = FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance()->GetActiveCamera();
        var renderCamera = controlCamera != null ? controlCamera->SceneCamera.RenderCamera : null;
        if (renderCamera != null)
        {
            Origin = renderCamera->Origin;
            View = renderCamera->ViewMatrix;
            View.M44 = 1;
            Proj = renderCamera->ProjectionMatrix;
            ViewProj = View * Proj;
            NearPlane = new(View.M13, View.M23, View.M33, View.M43 + renderCamera->NearPlane);
            CameraAzimuth = MathF.Atan2(View.M13, View.M33);
            CameraAltitude = MathF.Asin(View.M23);
            var device = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
            ViewportSize = new(device->Width, device->Height);
        }
        EffectMesh?.UpdateConstants(RenderContext, new() { ViewProj = ViewProj, CameraPos = Origin });
        _viewportLines.Clear();
        _viewportCircles.Clear();
    }

    public void EndFrame()
    {
        // Draw all collected shapes (lines and filled circles) to the screen
        foreach (var (center, radius, color) in _viewportCircles)
        {
            ImGui.GetWindowDrawList().AddCircleFilled(center, radius, (uint)color);
        }
        foreach (var (from, to, color, thickness) in _viewportLines)
        {
            ImGui.GetWindowDrawList().AddLine(from, to, (uint)color, thickness);
        }
    }

    public void DrawWorldLine(Vector3 start, Vector3 end, uint color, int thickness = 1)
    {
        if (ClipLineToNearPlane(ref start, ref end))
        {
            var s = WorldToScreen(start);
            var e = WorldToScreen(end);
            _viewportLines.Add((s, e, color, thickness));
        }
    }

    public void DrawWorldPolygon(IEnumerable<Vector3> points, uint color, int thickness = 1)
    {
        foreach (var (a, b) in AdjacentPairs(points))
        {
            DrawWorldLine(a, b, color, thickness);
        }
    }

    public void DrawWorldAABB(Vector3 origin, Vector3 halfSize, uint color, int thickness = 1)
    {
        var min = origin - halfSize;
        var max = origin + halfSize;
        Vector3[] corners = new Vector3[8];
        corners[0] = new(min.X, min.Y, min.Z);
        corners[1] = new(max.X, min.Y, min.Z);
        corners[2] = new(max.X, min.Y, max.Z);
        corners[3] = new(min.X, min.Y, max.Z);
        corners[4] = new(min.X, max.Y, min.Z);
        corners[5] = new(max.X, max.Y, min.Z);
        corners[6] = new(max.X, max.Y, max.Z);
        corners[7] = new(min.X, max.Y, max.Z);
        // bottom rectangle
        DrawWorldLine(corners[0], corners[1], color, thickness);
        DrawWorldLine(corners[1], corners[2], color, thickness);
        DrawWorldLine(corners[2], corners[3], color, thickness);
        DrawWorldLine(corners[3], corners[0], color, thickness);
        // top rectangle
        DrawWorldLine(corners[4], corners[5], color, thickness);
        DrawWorldLine(corners[5], corners[6], color, thickness);
        DrawWorldLine(corners[6], corners[7], color, thickness);
        DrawWorldLine(corners[7], corners[4], color, thickness);
        // vertical lines
        DrawWorldLine(corners[0], corners[4], color, thickness);
        DrawWorldLine(corners[1], corners[5], color, thickness);
        DrawWorldLine(corners[2], corners[6], color, thickness);
        DrawWorldLine(corners[3], corners[7], color, thickness);
    }

    public void DrawWorldAABB(AABB aabb, uint color, int thickness = 1)
        => DrawWorldAABB((aabb.Min + aabb.Max) * 0.5f, (aabb.Max - aabb.Min) * 0.5f, color, thickness);

    public void DrawWorldSphere(Vector3 center, float radius, uint color, int thickness = 1)
    {
        int numSegments = CurveApprox.CalculateCircleSegments(radius, 360.Degrees(), 0.1f);
        var prev1 = center + new Vector3(0, 0, radius);
        var prev2 = center + new Vector3(0, radius, 0);
        var prev3 = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= numSegments; ++i)
        {
            var dir = (i * 360.0f / numSegments).Degrees().ToDirection();
            var curr1 = center + radius * new Vector3(dir.X, 0, dir.Y);
            var curr2 = center + radius * new Vector3(0, dir.Y, dir.X);
            var curr3 = center + radius * new Vector3(dir.Y, dir.X, 0);
            DrawWorldLine(curr1, prev1, color, thickness);
            DrawWorldLine(curr2, prev2, color, thickness);
            DrawWorldLine(curr3, prev3, color, thickness);
            prev1 = curr1;
            prev2 = curr2;
            prev3 = curr3;
        }
    }

    public void DrawWorldTriangle(Vector3 v1, Vector3 v2, Vector3 v3, uint color, int thickness = 1)
    {
        DrawWorldLine(v1, v2, color, thickness);
        DrawWorldLine(v2, v3, color, thickness);
        DrawWorldLine(v3, v1, color, thickness);
    }

    public void DrawWorldPoint(Vector3 p, float radius, uint color, int thickness = 1)
    {
        if (Vector4.Dot(new Vector4(p, 1), NearPlane) >= 0)
            return;
        var screenPos = WorldToScreen(p);
        foreach (var (from, to) in AdjacentPairs(CurveApprox.Circle(screenPos, radius, 1)))
        {
            _viewportLines.Add((from, to, color, thickness));
        }
    }

    public void DrawWorldPointFilled(Vector3 p, float radius, uint color)
    {
        if (Vector4.Dot(new Vector4(p, 1), NearPlane) >= 0)
            return;
        _viewportCircles.Add((WorldToScreen(p), radius, color));
    }

    public void DrawWorldArrowPoint(Vector3 p, Vector3 q, float length, uint color, int thickness = 1)
    {
        if (Vector4.Dot(new Vector4(p, 1), NearPlane) >= 0)
            return;
        ClipLineToNearPlane(ref p, ref q);
        var ps = WorldToScreen(p);
        var qs = WorldToScreen(q);
        var d = Vector2.Normalize(qs - ps) * length;
        var n = new Vector2(-d.Y, d.X) * 0.5f;
        _viewportLines.Add((ps, ps + d + n, color, thickness));
        _viewportLines.Add((ps, ps + d - n, color, thickness));
    }

    public void DrawWorldArc(Vector3 a, Vector3 b, float height, float arrowA, float arrowB, uint color, int thickness = 1)
    {
        var delta = b - a;
        var len = delta.Length();
        height *= len;
        // draw arc as two segments
        var mid = a + delta * 0.5f + new Vector3(0, height, 0);
        DrawWorldLine(a, mid, color, thickness);
        DrawWorldLine(mid, b, color, thickness);
        // draw arrows at endpoints
        var dirAM = Vector3.Normalize(mid - a);
        var dirMB = Vector3.Normalize(b - mid);
        DrawWorldArrowPoint(a + dirAM * arrowA, a, arrowB, color, thickness);
        DrawWorldArrowPoint(b + dirMB * arrowA, b, arrowB, color, thickness);
    }

    // Draw all dead zones (as wireframe spheres) for visualization
    public void DrawDeadZones(IEnumerable<DeadZone> zones, uint color = 0xFFFF0000, int thickness = 2)
    {
        foreach (var zone in zones)
        {
            DrawWorldSphere(new Vector3(zone.X, zone.Y, zone.Z), zone.Radius, color, thickness);
        }
    }

    private bool ClipLineToNearPlane(ref Vector3 a, ref Vector3 b)
    {
        var an = Vector4.Dot(new Vector4(a, 1), NearPlane);
        var bn = Vector4.Dot(new Vector4(b, 1), NearPlane);
        if (an >= 0 && bn >= 0)
            return false; // line is fully behind the near plane (not visible)

        if (an > 0 || bn > 0)
        {
            var ab = b - a;
            var abn = Vector3.Dot(ab, new Vector3(NearPlane.X, NearPlane.Y, NearPlane.Z));
            var t = -an / abn;
            var p = a + t * ab;
            if (an > 0) a = p; else b = p;
        }
        return true;
    }

    private Vector2 WorldToScreen(Vector3 w)
    {
        var pp = Vector4.Transform(w, ViewProj);
        var iw = 1.0f / pp.W;
        return new Vector2(
            0.5f * ViewportSize.X * (1 + pp.X * iw),
            0.5f * ViewportSize.Y * (1 - pp.Y * iw)
        ) + ImGuiHelpers.MainViewport.Pos;
    }

    private static IEnumerable<(T, T)> AdjacentPairs<T>(IEnumerable<T> vertices) where T : struct
    {
        var en = vertices.GetEnumerator();
        if (!en.MoveNext())
            yield break;
        var first = en.Current;
        var prev = en.Current;
        while (en.MoveNext())
        {
            yield return (prev, en.Current);
            prev = en.Current;
        }
        yield return (prev, first);
    }
}
