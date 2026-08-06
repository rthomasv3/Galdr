using GaldrJson;

namespace Galdr.Native;

/// <summary>
/// Class used to dispatch events on the frontend.
/// </summary>
[GaldrJsonIgnore]
public sealed class EventService : IEventService
{
    #region Fields

    private readonly Galdr _galdr;

    #endregion

    #region Constructor

    internal EventService(Galdr galdr)
    {
        _galdr = galdr;
    }

    #endregion

    #region Public Methods

    /// <inheritdoc />
    public void PublishEvent(string eventName, string args)
    {
        string js = $"window.dispatchEvent(new CustomEvent('{eventName}', {{ detail: {args} }}));";
        _galdr.Evaluate(js);
    }

    #endregion
}
