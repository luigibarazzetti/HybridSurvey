using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eto.Forms;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Command = Rhino.Commands.Command;
using OpenFileDialog = Eto.Forms.OpenFileDialog;
using DialogResult = Eto.Forms.DialogResult;
using Form = Eto.Forms.Form;
using Label = Eto.Forms.Label;
using Button = Eto.Forms.Button;

namespace HybridSnap;

// Only point clouds imported with HybridCloudImport are controlled by HybridCloudShow.
// Other Rhino geometry and point clouds remain untouched.
internal static class ImportedClouds
{
    private static readonly HashSet<Guid> Ids = new();
    internal static bool Visible { get; private set; } = true;

    internal static int Import(RhinoDoc doc, string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Cloud file not found", path);
        var before = new HashSet<Guid>(doc.Objects.GetObjectList(ObjectType.PointSet).Select(o => o.Id));
        // Delegate format support to Rhino's installed file importers.
        // A failed/unsupported import is reported rather than silently claiming success.
        var escaped = path.Replace("\"", "\"\"");
        bool ran = RhinoApp.RunScript($"_-Import \"{escaped}\" _Enter", false);
        var added = doc.Objects.GetObjectList(ObjectType.PointSet)
            .Where(o => !before.Contains(o.Id)).Select(o => o.Id).ToArray();
        foreach (var id in added) Ids.Add(id);
        Visible = true;
        doc.Views.Redraw();
        RhinoApp.WriteLine($"Cloud import: {added.Length} point-cloud object(s) added from {Path.GetFileName(path)}.");
        if (!ran || added.Length == 0)
            RhinoApp.WriteLine("No point-cloud objects detected. Check that Rhino supports this file format and that it contains a point cloud.");
        return added.Length;
    }

    internal static int Toggle(RhinoDoc doc)
    {
        var objects = Ids.Select(doc.Objects.FindId).Where(o => o != null).ToArray();
        if (objects.Length == 0) return 0;
        bool show = !Visible;
        foreach (var obj in objects)
        {
            if (obj == null) continue;
            if (show) doc.Objects.Show(obj, true);
            else doc.Objects.Hide(obj, true);
        }
        Visible = show;
        doc.Views.Redraw();
        return objects.Length;
    }
}

public sealed class HybridCloudImportCommand : Command
{
    public override string EnglishName => "HybridCloudImport";
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        var dlg = new OpenFileDialog { Title = "Import point cloud using Rhino" };
        dlg.Filters.Add(new FileFilter("Point clouds", ".e57", ".las", ".laz", ".ply", ".xyz", ".pts", ".ptx", ".asc", ".txt"));
        dlg.Filters.Add(new FileFilter("All files", ".*"));
        if (dlg.ShowDialog(null) != DialogResult.Ok) return Result.Cancel;
        try { return ImportedClouds.Import(doc, dlg.FileName) > 0 ? Result.Success : Result.Failure; }
        catch (Exception ex) { RhinoApp.WriteLine("HybridCloudImport: " + ex.Message); return Result.Failure; }
    }
}

public sealed class HybridCloudShowCommand : Command
{
    public override string EnglishName => "HybridCloudShow";
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        int count = ImportedClouds.Toggle(doc);
        if (count == 0)
        {
            RhinoApp.WriteLine("No imported point clouds to toggle. Use HybridCloudImport first.");
            return Result.Nothing;
        }
        RhinoApp.WriteLine($"Cloud objects: {count}; visible: {ImportedClouds.Visible}.");
        return Result.Success;
    }
}
