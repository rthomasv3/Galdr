using GaldrJson;

namespace Galdr.Native;

/// <summary>
/// Mobile placeholder for the dialog service. The desktop synchronous, path-returning
/// dialog API cannot be honestly implemented over the async, URI-based pickers on
/// Android and iOS, so every method returns <c>null</c> on mobile. An async,
/// stream-oriented mobile file API is planned for a later release. Note that
/// <c>&lt;input type="file"&gt;</c> in the webview already works natively on both
/// platforms with no framework code.
/// </summary>
[GaldrJsonIgnore]
public sealed class DialogService : IDialogService
{
    #region Constructor

    internal DialogService()
    {
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Not supported on mobile — always returns <c>null</c>.
    /// </summary>
    public string OpenDirectoryDialog(string defaultPath = null)
    {
        return null;
    }

    /// <summary>
    /// Not supported on mobile — always returns <c>null</c>.
    /// </summary>
    public string OpenFileDialog(string filterList = null, string defaultPath = null)
    {
        return null;
    }

    /// <summary>
    /// Not supported on mobile — always returns <c>null</c>.
    /// </summary>
    public string[] OpenFileDialogMultiple(string filterList = null, string defaultPath = null)
    {
        return null;
    }

    /// <summary>
    /// Not supported on mobile — always returns <c>null</c>.
    /// </summary>
    public string OpenSaveDialog(string filterList = null, string defaultPath = null, string defaultName = null)
    {
        return null;
    }

    #endregion
}
