using System;

namespace Galdr.Native;

public interface IGaldrJsonSerializer
{
    string Serialize(object value, Type type);
    object Deserialize(string json, Type type);
    bool CanSerialize(Type type);
}
