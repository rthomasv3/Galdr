namespace Galdr.Native;

/// <summary>
/// Interface used to define the features of a dialog service.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Opens a system directory selection dialog.
    /// </summary>
    /// <returns>
    /// The path of the directory or null if cancelled.
    /// </returns>
    string OpenDirectoryDialog(string defaultPath = null);

    /// <summary>
    /// Opens a system file selection dialog.
    /// </summary>
    /// <returns>
    /// The path of the file or null if cancelled.
    /// </returns>
    string OpenFileDialog(string filterList = null, string defaultPath = null);

    /// <summary>
    /// Opens a system multi-file selection dialog.
    /// </summary>
    /// <returns>
    /// The paths of the files or null if cancelled.
    /// </returns>
    string[] OpenFileDialogMultiple(string filterList = null, string defaultPath = null);

    /// <summary>
    /// Opens a system file save dialog.
    /// </summary>
    /// <returns>
    /// The path of the file or null if cancelled.
    /// </returns>
    string OpenSaveDialog(string filterList = null, string defaultPath = null);
}
