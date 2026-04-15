using GaldrJson;

namespace Galdr.Native;

/// <summary>
/// Payload sent over the single-instance pipe from a duplicate process to the primary.
/// </summary>
[GaldrJsonSerializable]
internal sealed class SingleInstanceMessage
{
    public string Type { get; set; }
    public string[] Args { get; set; }
    public string Cwd { get; set; }
}
