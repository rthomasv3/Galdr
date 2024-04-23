using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Galdr;
using GaldrTestHarness.Commands.Models;

namespace GaldrTestHarness.Commands;

[Commands]
internal sealed class TestCommands
{
    #region Fields

    private readonly EventService _eventService;

    #endregion

    #region Constructor

    public TestCommands(EventService eventService)
    {
        _eventService = eventService;
    }

    #endregion

    #region Public Methods

    public bool TestingMethod()
    {
        return true;
    }

    public bool TestingMethodInt(int x)
    {
        return x != default;
    }

    public bool TestingMethodInts(int x, int y)
    {
        return x != default && y != default;
    }

    public bool TestingMethodString(string name)
    {
        return !String.IsNullOrWhiteSpace(name);
    }

    public bool TestingMethodStrings(string name, string address)
    {
        return !String.IsNullOrWhiteSpace(name) && !String.IsNullOrWhiteSpace(address);
    }

    public bool TestingMethodGuid(Guid id)
    {
        return id != default;
    }

    public bool TestingMethodGuids(Guid id1, Guid id2)
    {
        return id1 != default && id2 != default;
    }

    public bool TestingMethodDateTime(DateTime dateTime1)
    {
        return dateTime1 != default;
    }

    public bool TestingMethodDateTimes(DateTime dateTime1, DateTime dateTime2)
    {
        return dateTime1 != default && dateTime2 != default;
    }

    public string UTF8Test()
    {
        return "“This is a test” … — ™ ®";
    }

    public bool ModelParameterTest(TestModel model)
    {
        return model.Id != default && !String.IsNullOrWhiteSpace(model.Name);
    }

    public TestModel ModelReturnTest()
    {
        return new TestModel()
        {
            Id = 1,
            Name = "Test"
        };
    }

    public bool DITest(DialogService dialogService)
    {
        return dialogService != null;
    }

    public bool DynamicTest(dynamic param)
    {
        return param != null;
    }

    [Command(prefixClassName: true)]
    public bool PrefixTest()
    {
        return true;
    }

    public async Task RunAllUnitTests()
    {
        string workingDirectory = Path.Combine(MoveUpDirectory(Directory.GetCurrentDirectory(), 4), "Galdr.Tests");

        ProcessStartInfo processStartInfo = new ProcessStartInfo("dotnet", "test -v n")
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workingDirectory,
        };

        Process unitTestProcess = Process.Start(processStartInfo);

        unitTestProcess.OutputDataReceived += (_, args) =>
        {
            _eventService.PublishEvent("unitTestResults", args.Data);
        };

        unitTestProcess.ErrorDataReceived += (_, args) =>
        {
            _eventService.PublishEvent("unitTestResults", args.Data);
        };

        unitTestProcess.BeginOutputReadLine();
        unitTestProcess.BeginErrorReadLine();

        await unitTestProcess.WaitForExitAsync();
    }

    #endregion

    #region Private Methods

    private string MoveUpDirectory(string directory, int count = 1)
    {
        string parentDirectory = directory;

        while (count-- > 0)
        {
            parentDirectory = parentDirectory[..parentDirectory.LastIndexOf(Path.DirectorySeparatorChar)];
        }

        return parentDirectory;
    }

    #endregion
}
