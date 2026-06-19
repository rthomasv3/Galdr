# GaldrApp

A cross-platform desktop app built with [Galdr.Native](https://www.nuget.org/packages/Galdr.Native):
a web front end running in the OS native webview, with C# application logic behind a
typed JS-to-C# bridge.

This starter ships a working **greeter**: type a name, click **Greet**, and the text makes
a round trip to a C# command that returns `Hello, <name>`.

## Prerequisites

- The **.NET SDK** matching this project's target framework.
- **Node.js** 20.19+ or 22.12+ (required by Vite 8) for the front-end build.
- **Linux only:** the system WebKitGTK runtime - e.g. `webkit2gtk4.1` (Fedora) or
  `libwebkit2gtk-4.1-0` (Debian/Ubuntu). Windows (WebView2) and macOS (WKWebView) need nothing extra.

## Run it

### Debug - hot reload

Debug points the webview at the Vite dev server, so run both:

```bash
# 1) front-end dev server (leave running)
cd FrontEnd
npm install
npm run dev

# 2) the app, in another shell
dotnet run -c Debug
```

### Release - one command

Release builds the front end and stages it next to the executable automatically:

```bash
dotnet run -c Release
```

## Project layout

```
GaldrApp.csproj        # app project + the front-end build wiring (Release)
Program.cs             # GaldrBuilder setup: window, content provider, DI, commands
GreetingService.cs     # sample service (the "Hello, <name>" logic)
GreetingCommands.cs    # registers the `greet` command (object in, object out)
FrontEnd/              # the web UI (Vite)
  src/
    App.*              # the greeter UI
    invoke.*           # thin wrapper around the global galdrInvoke
    greetingService.*  # calls the `greet` command
```

## Add your own command

1. Add a handler in a `*Commands.cs` extension method:

   ```csharp
   builder.AddFunction("myCommand", (MyRequest request, MyService svc) => svc.DoWork(request));
   ```

2. Call it from the front end (args keyed by the handler's parameter names):

   ```js
   const result = await galdrInvoke('myCommand', { request: { /* ... */ } });
   ```

See the Galdr.Native guide for events, native dialogs, window control, and lifecycle hooks.
