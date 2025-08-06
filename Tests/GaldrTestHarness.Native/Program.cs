using System;
using System.Collections.Generic;
using System.Diagnostics;
using Galdr.Native;

namespace GaldrTestHarness.Native;

internal class Program
{
    [STAThread]
    static void Main()
    {
        GaldrBuilder builder = new GaldrBuilder()
            .SetTitle("Galdr Native Test Harness")
            .SetSize(1024, 768)
            .SetMinSize(800, 600);

#if DEBUG
        builder.SetPort(42069);
        builder.SetDebug(true);
#endif

        builder.AddAction("commandTest1", () =>
        {
            Debug.WriteLine("Command Test 1!");
        });

        builder.AddAction("commandTest2", (int x) =>
        {
            Debug.WriteLine($"Command Test 2 {x}!");
        });

        builder.AddFunction("commandTest3", (int x) =>
        {
            Debug.WriteLine($"Command Test 3 {x}!");

            return new TestResult
            {
                Result = x,
                Test2 = new TestResult2()
                {
                    Testing = "this is a test",
                    Test3 = new List<TestResult3>()
                    {
                        new TestResult3
                        {
                            Id = Guid.NewGuid(),
                            Time = DateTime.Now,
                        }
                    }
                }
            };
        });

        builder.AddSingleton<PrintService>();
        builder.AddFunction("commandTest4", (int count, PrintRequest request, PrintService printService, DialogService dialogService) =>
        {
            for (int i = 0; i < count; ++i)
            {
                printService.Print(request.Id, request.Name);
            }

            dialogService.OpenFileDialog();

            return new PrintResponse
            {
                Success = true,
            };
        });

        using Galdr.Native.Galdr galdr = builder
            .Build()
            .Run();
    }
}

public class TestResult
{
    public int Result { get; set; }

    public TestResult2 Test2 { get; set; }
}

public class TestResult2
{
    public string Testing { get; set; }
    public List<TestResult3> Test3 { get; set; }
}

public class TestResult3
{
    public DateTime Time { get; set; }
    public Guid Id { get; set; }
}

public class PrintRequest
{
    public Guid Id { get; set;}
    public string Name { get; set; }
}

public class PrintResponse
{
    public bool Success { get; set; }
}

public class PrintService()
{
    public void Print(Guid id, string name)
    {
        Debug.WriteLine($"{DateTime.Now:g}: {id} - {name}");
    }
}
