using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GaldrJson;

namespace Galdr.Native;

/// <summary>
/// Owns the single-instance lock and the IPC pipe used by duplicate launches to
/// notify the primary. In a primary process, starts a background listener that
/// focuses the window and invokes the user-supplied <see cref="GaldrOptions.SecondInstance"/>
/// callback when a duplicate launch is detected. In a duplicate process, sends a
/// payload to the primary and exits.
/// </summary>
internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string ActivateMessageType = "activate";

    private readonly string _appId;
    private readonly string _pipeName;
    private readonly SingleInstanceLock _lock;
    private readonly CancellationTokenSource _cts;

    private Galdr _galdr;
    private Action<string[], string> _secondInstanceHandler;
    private Task _listenTask;

    public SingleInstanceCoordinator(string appId)
    {
        _appId = appId;
        _pipeName = BuildPipeName(appId);
        _lock = new SingleInstanceLock(BuildLockPath(appId));
        _cts = new CancellationTokenSource();
    }

    public bool IsPrimary { get; private set; }

    public bool TryAcquire()
    {
        IsPrimary = _lock.TryAcquire();
        return IsPrimary;
    }

    public void SendActivateToPrimary()
    {
        SingleInstanceMessage message = new SingleInstanceMessage
        {
            Type = ActivateMessageType,
            Args = Environment.GetCommandLineArgs(),
            Cwd = Environment.CurrentDirectory,
        };

        try
        {
            using NamedPipeClientStream client = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.Out);
            client.Connect(500);

            string json = GaldrJson.GaldrJson.Serialize(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
        }
        catch
        {
        }
    }

    public void StartListener(Galdr galdr, Action<string[], string> secondInstanceHandler)
    {
        _galdr = galdr;
        _secondInstanceHandler = secondInstanceHandler;
        _listenTask = Task.Run(() => Listen(_cts.Token));
    }

    public void Dispose()
    {
        _cts.Cancel();

        if (_listenTask != null)
        {
            try
            {
                _listenTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
            }
        }

        _cts.Dispose();
        _lock.Dispose();
    }

    private async Task Listen(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using NamedPipeServerStream server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(token);

                using StreamReader reader = new StreamReader(server);
                string json = await reader.ReadToEndAsync(token);

                if (!String.IsNullOrEmpty(json))
                {
                    HandleIncoming(json);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                try
                {
                    await Task.Delay(500, token);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }

    private void HandleIncoming(string json)
    {
        SingleInstanceMessage message;

        try
        {
            message = GaldrJson.GaldrJson.Deserialize<SingleInstanceMessage>(json);
        }
        catch
        {
            message = null;
        }

        if (message != null && message.Type == ActivateMessageType)
        {
            _galdr.Dispatch(() =>
            {
                WindowActivator.Activate(_galdr.GetWindow());

                if (_secondInstanceHandler != null)
                {
                    try
                    {
                        _secondInstanceHandler(message.Args ?? Array.Empty<string>(), message.Cwd ?? String.Empty);
                    }
                    catch
                    {
                    }
                }
            });
        }
    }

    private static string BuildPipeName(string appId)
    {
        return $"galdr-{appId}-{Environment.UserName}";
    }

    private static string BuildLockPath(string appId)
    {
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, "Galdr", appId, "instance.lock");
    }
}
