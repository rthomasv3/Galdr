using System.Linq;
using System.Threading.Tasks;
using NativeFileDialogSharp;

namespace Galdr;

/// <summary>
/// Class used to provide access to native system dialogs.
/// </summary>
public sealed class DialogService
{
    /// <summary>
    /// Opens a system directory selection dialog.
    /// </summary>
    /// <returns>
    /// The path of the directory or null if cancelled.
    /// </returns>
    public string OpenDirectoryDialog(string defaultPath = null)
    {
        string directory = null;

        DialogResult result = Dialog.FolderPicker(defaultPath);

        if (result.IsOk)
        {
            directory = result.Path;
        }

        return directory;
    }

    /// <summary>
    /// Opens a system file selection dialog.
    /// </summary>
    /// <returns>
    /// The path of the file or null if cancelled.
    /// </returns>
    public string OpenFileDialog(string filterList = null, string defaultPath = null)
    {
        string file = null;

        DialogResult result = Dialog.FileOpen(filterList, defaultPath);

        if (result.IsOk)
        {
            file = result.Path;
        }

        return file;
    }

    /// <summary>
    /// Opens a system multi-file selection dialog.
    /// </summary>
    /// <returns>
    /// The paths of the files or null if cancelled.
    /// </returns>
    public string[] OpenFileDialogMultiple(string filterList = null, string defaultPath = null)
    {
        string[] files = null;

        DialogResult result = Dialog.FileOpenMultiple(filterList, defaultPath);

        if (result.IsOk)
        {
            files = result.Paths.ToArray();
        }

        return files;
    }

    /// <summary>
    /// Opens a system file save dialog.
    /// </summary>
    /// <returns>
    /// The path of the file or null if cancelled.
    /// </returns>
    public string OpenSaveDialog(string filterList = null, string defaultPath = null)
    {
        string file = null;

        DialogResult result = Dialog.FileSave(filterList, defaultPath);

        if (result.IsOk)
        {
            file = result.Path;
        }

        return file;
    }
}
