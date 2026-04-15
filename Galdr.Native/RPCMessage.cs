using GaldrJson;

namespace Galdr.Native;

[GaldrJsonSerializable]
internal sealed class RPCMessage
{
    public string Message { get; set; }
}
