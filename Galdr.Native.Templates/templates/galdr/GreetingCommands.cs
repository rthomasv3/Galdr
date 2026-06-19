using Galdr.Native;

namespace GaldrApp;

internal static class GreetingCommands
{
    public static GaldrBuilder AddGreetingCommands(this GaldrBuilder builder)
    {
        builder.AddFunction("greet", (GreetRequest request, GreetingService greetings) =>
            new GreetResponse { Message = greetings.Greet(request.Name) });

        return builder;
    }
}
