using System;

namespace Galdr.Native;

/// <summary>
/// Interface used to represent Galdr serialization features.
/// </summary>
public interface IGaldrJsonSerializer
{
    /// <summary>
    /// Serializes an object of the given type.
    /// </summary>
    string Serialize(object value, Type type);

    /// <summary>
    /// Deserializes an object of the given type.
    /// </summary>
    object Deserialize(string json, Type type);

    /// <summary>
    /// Returns true if the type can be serialized.
    /// </summary>
    bool CanSerialize(Type type);
}
