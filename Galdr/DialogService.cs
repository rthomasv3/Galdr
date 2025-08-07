using System.Linq;
using NativeFileDialogSharp;

namespace Galdr;

/// <summary>
/// Class used to provide access to native system dialogs.
/// </summary>
public sealed class DialogService : IDialogService
{
    /// <inheritdoc />
    public string OpenDirectoryDialog(string defaultPath = null)
    {
        string directory = null;

        DialogResult result = Dialog.FolderPicker(defaultPath);

        if (result?.IsOk == true)
        {
            directory = result.Path;
        }

        return directory;
    }

    /// <inheritdoc />
    public string OpenFileDialog(string filterList = null, string defaultPath = null)
    {
        string file = null;

        DialogResult result = Dialog.FileOpen(filterList, defaultPath);

        if (result?.IsOk == true)
        {
            file = result.Path;
        }

        return file;
    }

    /// <inheritdoc />
    public string[] OpenFileDialogMultiple(string filterList = null, string defaultPath = null)
    {
        string[] files = null;

        DialogResult result = Dialog.FileOpenMultiple(filterList, defaultPath);

        if (result?.IsOk == true)
        {
            files = result.Paths.ToArray();
        }

        return files;
    }

    /// <inheritdoc />
    public string OpenSaveDialog(string filterList = null, string defaultPath = null)
    {
        string file = null;

        DialogResult result = Dialog.FileSave(filterList, defaultPath);

        if (result?.IsOk == true)
        {
            file = result.Path;
        }

        return file;
    }
}
