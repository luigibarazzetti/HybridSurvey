using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using RhinoGaussianSplatting;

namespace HybridSnap;

/// <summary>
/// Licensed integration of the original Rhino Gaussian Splatting rendering core.
/// The third-party assembly is bundled privately as a DLL rather than installed
/// as a second Rhino plug-in. The original GL renderer owns its own conduit.
/// No sampled PointCloud proxy is created: Rhino Osnaps cannot snap to it.
/// </summary>
public static class LicensedGaussianRenderer
{
    private static string? _source;
    private static int _count;
    private static Splat[]? _originalSplats;
    private static int _opacityPercent = 100;
    private static int _savedOpacityPercent = 100;
    private static bool _xrayAppearance;
    // A screen-space selection is evaluated once and then stored per source splat.
    // The mask remains stable as the user navigates to a different view.
    private static bool[]? _polygonInside;
    private static bool _hideInside = true;
    private static int _polygonSelectedCount;
    public static bool HasPolygonMask => _polygonInside != null;
    public static string PolygonStatus => _polygonInside == null
        ? "No polygon mask" : $"Polygon mask: {_polygonSelectedCount:N0} selected, {(_hideInside ? "hide inside" : "hide outside")}";


    public static bool IsLoaded => _originalSplats != null;
    public static bool IsVisible => ((DisplayConduit)SplatDisplayConduit.Instance).Enabled;
    public static int OpacityPercent => _opacityPercent;
    public static bool XRayAppearance => _xrayAppearance;

    // Combine opacity and polygon selection in a single rebuild. The source array
    // remains immutable, allowing full restoration without re-reading the PLY.
    private static void RebuildVisible(RhinoDoc doc)
    {
        if (_originalSplats == null) return;
        var source = _originalSplats;
        if (_opacityPercent == 0)
        {
            SplatDisplayConduit.DisableConduit();
            doc.Views.Redraw();
            return;
        }
        var watch = Stopwatch.StartNew();
        int visibleCount = source.Length;
        if (_polygonInside != null)
            visibleCount = _hideInside ? source.Length - _polygonSelectedCount : _polygonSelectedCount;
        if (visibleCount < 0 || visibleCount > source.Length)
            throw new InvalidOperationException("Invalid polygon selection count.");
        var visible = new Splat[visibleCount];
        int write = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (_polygonInside != null && (_hideInside ? _polygonInside[i] : !_polygonInside[i]))
                continue;
            var splat = source[i];
            if (_opacityPercent != 100)
                splat.A = (byte)Math.Clamp((int)Math.Round(splat.A * (_opacityPercent / 100.0)), 0, 255);
            visible[write++] = splat;
        }
        SplatDisplayConduit.DisableConduit();
        if (visible.Length > 0)
            SplatDisplayConduit.EnableConduit(visible);
        doc.Views.Redraw();
        RhinoApp.WriteLine($"Gaussian renderer: {visible.Length:N0}/{source.Length:N0} visible, opacity {_opacityPercent}%, updated in {watch.Elapsed.TotalSeconds:0.0}s.");
    }

    public static void SetOpacity(RhinoDoc doc, int percent)
    {
        if (_originalSplats == null)
        {
            RhinoApp.WriteLine("Load Gaussian PLY before changing opacity.");
            return;
        }
        _opacityPercent = Math.Clamp(percent, 0, 100);
        RebuildVisible(doc);
    }

    public static bool ApplyPolygonMask(RhinoDoc doc, IReadOnlyList<Point2d> polygon, bool hideInside)
    {
        if (_originalSplats == null || polygon.Count < 3) return false;
        var view = doc.Views.ActiveView;
        if (view == null) return false;
        var viewport = view.ActiveViewport;
        var mask = new bool[_originalSplats.Length];
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in polygon)
        {
            minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
        }
        var watch = Stopwatch.StartNew();
        int count = 0;
        for (int i = 0; i < _originalSplats.Length; i++)
        {
            var s = _originalSplats[i];
            var screen = viewport.WorldToClient(new Point3d(s.X, s.Y, s.Z));
            if (screen.X < minX || screen.X > maxX || screen.Y < minY || screen.Y > maxY)
                continue;
            if (!InsidePolygon(screen.X, screen.Y, polygon)) continue;
            mask[i] = true;
            count++;
        }
        _polygonInside = mask;
        _polygonSelectedCount = count;
        _hideInside = hideInside;
        RhinoApp.WriteLine($"Polygon evaluated: {count:N0}/{mask.Length:N0} splat centers inside ({watch.Elapsed.TotalSeconds:0.0}s). Selection stays fixed in 3D.");
        RebuildVisible(doc);
        return true;
    }

    private static bool InsidePolygon(double x, double y, IReadOnlyList<Point2d> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i]; var b = polygon[j];
            if ((a.Y > y) != (b.Y > y) &&
                x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    public static void ClearPolygonMask(RhinoDoc doc)
    {
        if (_polygonInside == null) return;
        _polygonInside = null;
        _polygonSelectedCount = 0;
        RebuildVisible(doc);
        RhinoApp.WriteLine("Polygon mask cleared. All source splats restored.");
    }

    public static void SetXRayAppearance(RhinoDoc doc, bool enabled)
    {
        if (enabled == _xrayAppearance) return;
        if (enabled)
        {
            if (_originalSplats == null) { RhinoApp.WriteLine("Load Gaussian PLY first."); return; }
            _savedOpacityPercent = _opacityPercent;
            _xrayAppearance = true;
            SetOpacity(doc, 25);
        }
        else
        {
            _xrayAppearance = false;
            SetOpacity(doc, _savedOpacityPercent);
        }
    }

    public static string Status
    {
        get
        {
            var conduit = SplatDisplayConduit.Instance;
            return $"Licensed Gaussian renderer: {(_count > 0 ? _count.ToString("N0") : "0")} splats; " +
                   $"visible={((DisplayConduit)conduit).Enabled}; opacity={_opacityPercent}%; xray={_xrayAppearance}; {PolygonStatus}; file={_source ?? "none"}";
        }
    }

    public static bool Import(RhinoDoc doc, string file)
    {
        if (doc == null || !File.Exists(file)) throw new FileNotFoundException("PLY not found", file);
        if (!string.Equals(Path.GetExtension(file), ".ply", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("This integration starts with standard 3DGS PLY files.");

        // All PLY I/O runs on CPU; no GL calls during file selection or loading.
        var splats = SplatLoader.LoadPly(file, out string error);
        if (splats == null || splats.Length == 0)
            throw new InvalidDataException("Cannot load PLY: " + error);

        // Keep original PLY coordinates unchanged. In particular, do NOT apply
        // the source plug-in's optional -90 degree X-axis import conversion.
        // E57 and splats must share their already co-registered coordinate system.
        SplatDisplayConduit.DisableConduit();
        SplatDisplayConduit.EnableConduit(splats);
        _originalSplats = splats;
        _polygonInside = null;
        _polygonSelectedCount = 0;
        _opacityPercent = 100;
        _savedOpacityPercent = 100;
        _xrayAppearance = false;
        _source = file;
        _count = splats.Length;
        doc.Views.Redraw();
        RhinoApp.WriteLine(Status);
        return true;
    }

    public static void Show(RhinoDoc doc, bool visible)
    {
        var conduit = SplatDisplayConduit.Instance;
        if (_count == 0) { RhinoApp.WriteLine("No Gaussian PLY loaded. Use HybridSplatImport."); return; }
        ((DisplayConduit)conduit).Enabled = visible;
        doc.Views.Redraw();
        RhinoApp.WriteLine(Status);
    }

    public static void Clear(RhinoDoc doc)
    {
        SplatDisplayConduit.DisableConduit();
        _source = null;
        _count = 0;
        _originalSplats = null;
        _polygonInside = null;
        _polygonSelectedCount = 0;
        _opacityPercent = 100;
        _xrayAppearance = false;
        doc.Views.Redraw();
    }
}

public sealed class HybridSplatImportCommand : Command
{
    public override string EnglishName => "HybridSplatImport";
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var dlg = new Eto.Forms.OpenFileDialog { Title = "Import 3DGS PLY (licensed renderer)" };
        dlg.Filters.Add(new Eto.Forms.FileFilter("Gaussian PLY", ".ply"));
        if (dlg.ShowDialog(null) != Eto.Forms.DialogResult.Ok) return Result.Cancel;
        try { return LicensedGaussianRenderer.Import(doc, dlg.FileName) ? Result.Success : Result.Failure; }
        catch (Exception ex) { RhinoApp.WriteLine("HybridSplatImport: " + ex); return Result.Failure; }
    }
}
