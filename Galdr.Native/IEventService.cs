namespace Galdr.Native;

/// <summary>
/// Interface used to define the features of an event service.
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Dispatches a new CustomEvent of the given name on the main window and thread.
    /// </summary>
    void PublishEvent(string eventName, string args);
}
