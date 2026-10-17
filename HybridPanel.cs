using System;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.Commands;

namespace HybridSnap;

// Modeless control window: the Rhino viewport remains fully interactive.
internal sealed class HybridPanel : Form
{
    private static HybridPanel? _instance;
    private readonly Label _status;
    private readonly NumericStepper _opacity;

    private HybridPanel()
    {
        Title = "HybridSurvey | Gaussian + Cloud";
        ClientSize = new Size(370, 390);
        MinimumSize = new Size(330, 350);
        Resizable = true;
        Padding = new Padding(18);
        _status = new Label { Text = "Ready", Wrap = WrapMode.Word };
        _opacity = new NumericStepper { MinValue = 0, MaxValue = 100, Increment = 5, DecimalPlaces = 0, Value = LicensedGaussianRenderer.OpacityPercent };

        var importGs = MakeButton("Import Gaussian PLY", () => RhinoApp.RunScript("_HybridSplatImport", false));
        var importCloud = MakeButton("Import Point Cloud", () => RhinoApp.RunScript("_HybridCloudImport", false));
        var showGs = MakeButton("Show / Hide Gaussian", () => RhinoApp.RunScript("_HybridSplatShow", false));
        var showCloud = MakeButton("Show / Hide Cloud", () => RhinoApp.RunScript("_HybridCloudShow", false));
        var clearGs = MakeButton("Remove Gaussian", () => RhinoApp.RunScript("_HybridSplatClear", false));
        var applyOpacity = MakeButton("Apply Opacity", () =>
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc != null && LicensedGaussianRenderer.IsLoaded)
                LicensedGaussianRenderer.SetOpacity(doc, (int)Math.Round(_opacity.Value));
        });
        var refresh = MakeButton("Refresh Status", RefreshStatus);

        Content = new StackLayout
        {
            Spacing = 9,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Items =
            {
                new Label { Text = "HYBRIDSURVEY", Font = new Font(SystemFont.Bold, 17) },
                new Label { Text = "Single View  |  Display controls", TextColor = Colors.Gray },
                new StackLayout { Orientation = Orientation.Horizontal, Spacing = 8, Items = { new StackLayoutItem(importGs, true), new StackLayoutItem(importCloud, true) } },
                new StackLayout { Orientation = Orientation.Horizontal, Spacing = 8, Items = { new StackLayoutItem(showGs, true), new StackLayoutItem(showCloud, true) } },
                new Label { Text = "Gaussian opacity (0–100%)" },
                new StackLayout { Orientation = Orientation.Horizontal, Spacing = 8, Items = { new StackLayoutItem(_opacity, true), new StackLayoutItem(applyOpacity, true) } },
                clearGs,
                new Label { Text = "STATUS", Font = new Font(SystemFont.Bold, 10) },
                _status,
                refresh
            }
        };
        Closed += (_, _) => _instance = null;
        RefreshStatus();
    }

    private static Button MakeButton(string title, Action action)
    {
        var button = new Button { Text = title, Height = 36 };
        button.Click += (_, _) =>
        {
            try { action(); _instance?.RefreshStatus(); }
            catch (Exception ex) { RhinoApp.WriteLine("HybridSurvey: " + ex.Message); }
        };
        return button;
    }

    private void RefreshStatus()
    {
        _status.Text = LicensedGaussianRenderer.IsLoaded
            ? $"Gaussian: loaded | {(LicensedGaussianRenderer.IsVisible ? "visible" : "hidden")} | {LicensedGaussianRenderer.OpacityPercent}% opacity\nCloud: {(ImportedClouds.Visible ? "visible" : "hidden")}" 
            : $"Gaussian: not loaded\nCloud: {(ImportedClouds.Visible ? "visible" : "hidden")}";
        _opacity.Value = LicensedGaussianRenderer.OpacityPercent;
    }

    internal static void Open()
    {
        if (_instance != null) { _instance.BringToFront(); return; }
        _instance = new HybridPanel();
        _instance.Show();
    }
}

public sealed class HybridPanelCommand : Rhino.Commands.Command
{
    public override string EnglishName => "HybridPanel";
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        HybridPanel.Open();
        return Result.Success;
    }
}

public sealed class HybridSplatClearCommand : Rhino.Commands.Command
{
    public override string EnglishName => "HybridSplatClear";
    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (!LicensedGaussianRenderer.IsLoaded)
        {
            RhinoApp.WriteLine("No Gaussian Splatting loaded.");
            return Result.Nothing;
        }
        LicensedGaussianRenderer.Clear(doc);
        RhinoApp.WriteLine("Gaussian Splatting removed from renderer. Point clouds are unchanged.");
        return Result.Success;
    }
}
