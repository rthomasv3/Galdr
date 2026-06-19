using Galdr.Native;

namespace GaldrApp;

internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        GaldrBuilder builder = new GaldrBuilder()
            .SetTitle("GaldrApp")
            .SetSize(640, 480)
            .SetMinSize(420, 320);

//-:cnd:noEmit
#if DEBUG
        // URL must match the Vite dev server port (see vite.config).
        builder.SetDebug(true)
               .SetContentProvider(new UrlContent("http://localhost:5174"));
#else
        string wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        builder.SetContentProvider(new FolderContent(wwwroot));
#endif
//+:cnd:noEmit

        builder.AddSingleton<GreetingService>();
        builder.AddGreetingCommands();

        using Galdr.Native.Galdr galdr = builder.Build().Run();
    }
}
