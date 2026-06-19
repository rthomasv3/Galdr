using System;
using System.IO;
using System.Text;
using System.Threading;
using GaldrApp.Services.Abstractions;

namespace GaldrApp.Services;

public sealed class FileLoggingService : ILoggingService, IDisposable
{
    private const long MaxSizeBytes = 10L * 1024L * 1024L;
    private const long TrimTargetBytes = 8L * 1024L * 1024L;
    private const int MaxEntryScanBytes = 512 * 1024;

    private readonly string _filePath;
    private readonly SemaphoreSlim _writeLock;
    private bool _disposed;

    public FileLoggingService(Config config)
    {
        _filePath = config.LogFilePath;
        _writeLock = new SemaphoreSlim(1, 1);
    }

    public void Debug(string source, string message)
    {
        Write("DEBUG", source, message, null);
    }

    public void Info(string source, string message)
    {
        Write("INFO ", source, message, null);
    }

    public void Warn(string source, string message)
    {
        Write("WARN ", source, message, null);
    }

    public void Error(string source, string message, Exception ex = null)
    {
        Write("ERROR", source, message, ex);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _writeLock.Dispose();
        }
    }

    private void Write(string level, string source, string message, Exception ex)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        StringBuilder entry = new StringBuilder();
        entry.Append('[').Append(timestamp).Append("] [").Append(level).Append("] [").Append(source).Append("] - ").Append(message);

        if (ex != null)
        {
            string exceptionText = ex.ToString().Replace("\r\n", "\n");
            string[] lines = exceptionText.Split('\n');

            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    entry.Append('\n').Append("   ").Append(line);
                }
            }
        }

        entry.Append('\n');

        _writeLock.Wait();

        try
        {
            string entryString = entry.ToString();
            System.Diagnostics.Debug.WriteLine(entryString);
            File.AppendAllText(_filePath, entryString);

            FileInfo info = new FileInfo(_filePath);

            if (info.Exists && info.Length > MaxSizeBytes)
            {
                TrimFront(info.Length);
            }
        }
        catch
        {
            // Don't propagate logger failures.
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void TrimFront(long currentLength)
    {
        long cutoffOffset = currentLength - TrimTargetBytes;

        if (cutoffOffset > 0)
        {
            byte[] allBytes = File.ReadAllBytes(_filePath);
            int startIndex = FindNextEntryStart(allBytes, (int)cutoffOffset);

            if (startIndex > 0 && startIndex < allBytes.Length)
            {
                int remainingLength = allBytes.Length - startIndex;
                using FileStream fs = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
                fs.Write(allBytes, startIndex, remainingLength);
            }
        }
    }

    private static int FindNextEntryStart(byte[] bytes, int from)
    {
        int scanEnd = Math.Min(from + MaxEntryScanBytes, bytes.Length);
        int result = -1;

        for (int i = from; i < scanEnd - 1; i++)
        {
            if (bytes[i] == (byte)'\n' && bytes[i + 1] == (byte)'[')
            {
                result = i + 1;
                break;
            }
        }

        if (result == -1)
        {
            for (int i = from; i < scanEnd - 1; i++)
            {
                if (bytes[i] == (byte)'\n')
                {
                    result = i + 1;
                    break;
                }
            }
        }

        if (result == -1)
        {
            result = from;
        }

        return result;
    }
}
