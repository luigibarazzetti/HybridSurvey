using Rhino.PlugIns;
using System.Runtime.InteropServices;
namespace HybridSnap;
[Guid("7B41F0D2-5C8A-4E93-9F21-63A8D470B152")]
public sealed class HybridSnapPlugin : PlugIn
{
    public static HybridSnapPlugin Instance { get; private set; } = null!;
    public HybridSnapPlugin() { Instance = this; }
}
