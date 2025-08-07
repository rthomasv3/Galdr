using System;

namespace Galdr.Native;

/// <summary>
/// The serialization registry use to register IGaldrJsonSerializers.
/// </summary>
public static class GaldrJsonSerializerRegistry
{
    private static IGaldrJsonSerializer _serializer;

    /// <summary>
    /// Registers a new serializer.
    /// Called by generated code at module initialization
    /// </summary>
    public static void Register(IGaldrJsonSerializer serializer)
    {
        _serializer = serializer;
    }

    internal static bool TrySerialize(object value, Type actualType, out string json)
    {
        if (_serializer != null && _serializer.CanSerialize(actualType))
        {
            json = _serializer.Serialize(value, actualType);
            return true;
        }

        json = null!;
        return false;
    }

    internal static bool TrySerialize<T>(T value, out string json)
    {
        if (_serializer != null && _serializer.CanSerialize(typeof(T)))
        {
            json = _serializer.Serialize(value!, typeof(T));
            return true;
        }

        json = null!;
        return false;
    }

    internal static bool TryDeserialize(string json, Type targetType, out object value)
    {
        if (_serializer != null && _serializer.CanSerialize(targetType))
        {
            value = _serializer.Deserialize(json, targetType);
            return true;
        }

        value = null;
        return false;
    }

    internal static bool TryDeserialize<T>(string json, out T value)
    {
        if (_serializer != null && _serializer.CanSerialize(typeof(T)))
        {
            value = (T)_serializer.Deserialize(json, typeof(T));
            return true;
        }

        value = default!;
        return false;
    }
}
