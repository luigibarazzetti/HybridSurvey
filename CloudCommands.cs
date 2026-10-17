using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Command = Rhino.Commands.Command;

namespace HybridSnap;

// Tracks objects created by Rhino's native Import command, per document.
// The import dialog and E57/other file readers are managed entirely by Rhino.
internal static class ImportedClouds
{
    private static readonly Dictionary<uint, HashSet<Guid>> ImportedIds = new();
    private static readonly Dictionary<uint, bool> Visibility = new();

    internal static bool Visible
    {
        get
        {
            var doc = RhinoDoc.ActiveDoc;
            return doc == null || !Visibility.TryGetValue(doc.RuntimeSerialNumber, out var value) || value;
        }
    }

    internal static int Import(RhinoDoc doc)
    {
        var before = new HashSet<Guid>(doc.Objects.GetObjectList(ObjectType.AnyObject).Select(o => o.Id));
        // Do not supply a filename or an extra Enter: Rhino owns its native
        // file dialog, importer options and E57 conversion workflow.
        bool completed = RhinoApp.RunScript("_-Import", false);
        var added = doc.Objects.GetObjectList(ObjectType.AnyObject)
            .Where(o => !before.Contains(o.Id))
            .ToArray();

        if (added.Length == 0)
        {
            RhinoApp.WriteLine(completed
                ? "Rhino Import finished, but no new document objects were detected. Check Rhino's import messages."
                : "Rhino Import was cancelled or did not complete.");
            return 0;
        }

        // Importers may create point clouds, individual points, meshes or
        // instances. Preserve their original Rhino objects and group the
        // newly imported objects for Show/Hide without converting geometry.
        if (!ImportedIds.TryGetValue(doc.RuntimeSerialNumber, out var ids))
        {
            ids = new HashSet<Guid>();
            ImportedIds[doc.RuntimeSerialNumber] = ids;
        }
        foreach (var obj in added) ids.Add(obj.Id);
        Visibility[doc.RuntimeSerialNumber] = true;
        int pointClouds = added.Count(o => o.ObjectType == ObjectType.PointSet);
        RhinoApp.WriteLine($"Rhino Import: {added.Length} object(s) added ({pointClouds} point-cloud object(s)).");
        if (pointClouds == 0)
            RhinoApp.WriteLine("Note: Rhino imported objects of other types. HybridCloudShow will control these imported objects as well.");
        doc.Views.Redraw();
        return added.Length;
    }

    internal static int Toggle(RhinoDoc doc)
    {
        if (!ImportedIds.TryGetValue(doc.RuntimeSerialNumber, out var ids)) return 0;
        var objects = ids.Select(doc.Objects.FindId).Where(o => o != null && !o.IsDeleted).ToArray();
        if (objects.Length == 0) return 0;
        bool show = !Visible;
        foreach (var obj in objects)
        {
            if (obj == null) continue;
            if (show) doc.Objects.Show(obj, true);
            else doc.Objects.Hide(obj, true);
        }
        Visibility[doc.RuntimeSerialNumber] = show;
        doc.Views.Redraw();
        return objects.Length;
    }
}

public sealed class HybridCloudImportCommand : Command
{
    public override string EnglishName => "HybridCloudImport";
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try { return ImportedClouds.Import(doc) > 0 ? Result.Success : Result.Nothing; }
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
            RhinoApp.WriteLine("No imported objects to toggle. Use HybridCloudImport first.");
            return Result.Nothing;
        }
        RhinoApp.WriteLine($"Imported cloud objects: {count}; visible: {ImportedClouds.Visible}.");
        return Result.Success;
    }
}
