using System;

namespace Galdr.Native;

/// <summary>
/// Mutable holder that bridges the service provider built inside <see cref="Galdr"/> back
/// to command handlers captured by <see cref="GaldrBuilder"/>. Populated during
/// <see cref="Galdr.Run"/> after the provider is built.
/// </summary>
internal sealed class ServiceProviderAccessor
{
    public IServiceProvider Provider { get; set; }
}
