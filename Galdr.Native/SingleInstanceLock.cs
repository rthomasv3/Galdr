using System;
using System.IO;

namespace Galdr.Native;

/// <summary>
/// Process-scoped OS file lock. Held via an exclusive FileStream handle for the
/// lifetime of the owning process; released automatically on process death,
/// including hard crashes and forced termination (no stale-lock problem).
/// </summary>
internal sealed class SingleInstanceLock : IDisposable
{
    private readonly string _lockFilePath;
    private FileStream _lockStream;

    public SingleInstanceLock(string lockFilePath)
    {
        _lockFilePath = lockFilePath;
    }

    public bool TryAcquire()
    {
        bool acquired = false;

        try
        {
            string directory = Path.GetDirectoryName(_lockFilePath);

            if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _lockStream = new FileStream(
                _lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            acquired = true;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return acquired;
    }

    public void Dispose()
    {
        if (_lockStream != null)
        {
            _lockStream.Dispose();
            _lockStream = null;
        }
    }
}
