using System;
using Galdr;
using GaldrTestHarness.Commands;

namespace GaldrTestHarness;

internal class Program
{
    [STAThread]
    static void Main()
    {
        using Galdr.Galdr galdr = new GaldrBuilder()
            .SetTitle("Galdr Test Harness")
            .SetSize(1024, 768)
            .SetMinSize(800, 600)
            .AddSingleton<SingletonExample>()
            .AddService<TransientExample>()
            .AddService<CommandExamples>()
            .AddSingleton<TestCommands>()
            //.SetLoadingPage(loadingMessage: "Loading Galdr Test Harness", backgroundColor: "#0d0c0c")
            .SetCommandNamespace("Commands")
#if DEBUG
            .SetPort(42069)
            .SetDebug(true)
#endif
            .Build()
            .Run();
    }
}
