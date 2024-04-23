using System;
using System.Threading.Tasks;
using Galdr;

namespace GaldrTestHarness.Commands;

internal sealed class CommandExamples
{
    #region Fields

    private readonly SingletonExample _singletonExample;
    private readonly EventService _eventService;
    private readonly DialogService _dialogService;

    #endregion

    #region Constructor

    public CommandExamples(SingletonExample singletonExample, EventService eventService, DialogService dialogService)
    {
        _singletonExample = singletonExample;
        _eventService = eventService;
        _dialogService = dialogService;
    }

    #endregion

    #region Public Methods

    [Command]
    public async Task<string> TestAsync()
    {
        await Task.Delay(1000);
        int count = _singletonExample.Increment();
        return $"it worked async {count}";
    }

    [Command]
    public string TestSync(dynamic test)
    {
        int count = _singletonExample.Increment();
        return $"it worked sync {count}";
    }

    [Command]
    public string TestFailureSync()
    {
        throw new NotImplementedException("testing errors sync");
    }

    [Command]
    public async Task<string> TestFailureAsync()
    {
        await Task.Delay(1000);
        throw new NotImplementedException("testing errors async");
    }

    [Command]
    public void TestEvents()
    {
        _eventService.PublishEvent("testing", new { Test = "working!" });
    }

    [Command]
    public string BrowseDirectories()
    {
        return _dialogService.OpenDirectoryDialog();
    }

    [Command]
    public string BrowseFiles()
    {
        return _dialogService.OpenFileDialog();
    }

    [Command]
    public string SaveFile()
    {
        return _dialogService.OpenSaveDialog();
    }

    #endregion
}
