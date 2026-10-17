using Rhino;
using Rhino.Commands;
using Rhino.Input;

namespace HybridSnap;

public sealed class HybridSplatOpacityCommand : Command
{
    public override string EnglishName => "HybridSplatOpacity";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (!LicensedGaussianRenderer.IsLoaded)
        {
            RhinoApp.WriteLine("Import a Gaussian PLY first with HybridSplatImport.");
            return Result.Failure;
        }

        int opacity = LicensedGaussianRenderer.OpacityPercent;
        var result = RhinoGet.GetInteger("Gaussian opacity (0 = invisible, 100 = opaque)", false, ref opacity, 0, 100);
        if (result != Result.Success) return result;
        LicensedGaussianRenderer.SetOpacity(doc, opacity);
        return Result.Success;
    }
}

public sealed class HybridSplatShowCommand : Command
{
    public override string EnglishName => "HybridSplatShow";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        if (!LicensedGaussianRenderer.IsLoaded)
        {
            RhinoApp.WriteLine("Import a Gaussian PLY first with HybridSplatImport.");
            return Result.Failure;
        }
        LicensedGaussianRenderer.Show(doc, !LicensedGaussianRenderer.IsVisible);
        return Result.Success;
    }
}
