# Galdr.Native — Developer Guide

Galdr.Native is a multi-platform desktop application framework for C#. You build your
UI as a web application (any framework/bundler) and your application logic in C#.
Galdr hosts the UI in the operating system's native webview — WebView2 on Windows,
WKWebView on macOS, WebKitGTK on Linux — and provides a typed bridge between the two.

- **Targets:** .NET 8, 9, and 10. AOT-compatible (AOT is optional for your app).
- **You write:** a web front end + C# command handlers and services.
- **Galdr provides:** the window, the JS to C# bridge, dependency injection, native 
dialogs, events, window control, and app lifecycle hooks.

---

## 1. How it works

```
┌─────────────────────────────┐        galdrInvoke(name, args)         ┌──────────────────┐
│   Web UI (HTML/JS/CSS)      │  ───────────────────────────────────▶ │  C# command      │
│   running in native webview │  ◀─────────────────────────────────── │  handlers + DI   │
│                             │     Promise<result>  /  events         │  services        │
└─────────────────────────────┘                                        └──────────────────┘
```

1. Your web UI is served to the webview (from a dev server in debug, from local files in release).
2. The UI calls C# **commands** through a global `galdrInvoke` function and gets a `Promise` back.
3. C# can push **events** to the UI, open native **dialogs**, and control the **window**.

You configure everything through `GaldrBuilder`, then `Build().Run()`.

---

## 2. Getting started

The fastest path is the `dotnet new` template, which scaffolds the project, the JS/C#
bridge wiring, the front-end build, and a working sample. You can also wire Galdr into a
project by hand -- see "Manual setup" below.

### Prerequisites

- The **.NET 8, 9, or 10 SDK**.
- **Node.js** for the front-end build. The template uses Vite 8, which needs Node 20.19+ or 22.12+.
- **Linux only:** the system WebKitGTK runtime. Galdr bundles its own helper libraries
  under `runtimes/<rid>/native`, but on Linux the webview loads against the OS package.
  Install it if it's missing — e.g. `webkit2gtk4.1` (Fedora) or `libwebkit2gtk-4.1-0`
  (Debian/Ubuntu). Windows (WebView2) and macOS (WKWebView) need nothing extra.

### Quick start with the template

Galdr ships a `dotnet new` template. Install the pack once:

```bash
dotnet new install Galdr.Native.Templates
```

Scaffold an app (defaults to Vue + TypeScript on .NET 10):

```bash
dotnet new galdr -n MyApp
```

This generates a complete, runnable app: the window, the JS/C# bridge, dependency
injection, and a working "greeter" that round-trips text through a C# command -- plus the
front-end build wired into the project file.

Options:

| Option | Choices | Default | Effect |
|---|---|---|---|
| `--frontend` | `vue`, `react` | `vue` | Front-end framework. |
| `--js` | flag | off | Use JavaScript instead of TypeScript. |
| `-f` | `net8.0`, `net9.0`, `net10.0` | `net10.0` | Target framework. |
| `--aot` | flag | off | Enable `PublishAot`. |

```bash
dotnet new galdr -n MyApp --frontend react        # React + TypeScript
dotnet new galdr -n MyApp --frontend vue --js     # Vue + JavaScript
dotnet new galdr -n MyApp -f net8.0 --aot         # .NET 8, AOT enabled
```

**Run it.** In debug the app loads from the Vite dev server (hot reload); in release the
front end is built and served from disk.

```bash
# Debug: start the dev server, then run the app in another shell.
cd FrontEnd && npm install && npm run dev
dotnet run

# Release: one command builds the front end, stages it as wwwroot, and runs.
dotnet run -c Release
```

Generated layout:

```
MyApp/
  Program.cs             GaldrBuilder setup; debug/release content split
  GreetingService.cs     sample service ("Hello, <name>")
  GreetingCommands.cs    registers the greet command (object in, object out)
  GreetRequest.cs        request/response DTOs
  GreetResponse.cs
  FrontEnd/              Vite app (src/App.*, invoke.*, greetingService.*)
  MyApp.csproj           project + front-end build wiring
```

### Manual setup (without the template)

To wire Galdr into an existing project, or to see what the template generates, set it up
by hand. Galdr.Native ships on NuGet. From an empty folder:

```bash
dotnet new console -n MyApp -o .
dotnet add package Galdr.Native
```

Then **replace the entire generated `Program.cs`** (it defaults to top-level statements)
with the version below. The `[STAThread]` attribute is required — the native webview
expects a single-threaded apartment on Windows.

A minimal `Program.cs`:

```csharp
using Galdr.Native;

internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        GaldrBuilder builder = new GaldrBuilder()
            .SetTitle("My App")
            .SetSize(1280, 800)
            .SetMinSize(800, 600);

#if DEBUG
        // Point at your front-end dev server (e.g. Vite) for hot reload.
        builder.SetDebug(true)
               .SetContentProvider(new UrlContent("http://localhost:5174"));
#else
        // Serve the built front end from disk, no HTTP server.
        string wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        builder.SetContentProvider(new FolderContent(wwwroot));
#endif

        // Register your services and commands (see sections 4 and 5).
        builder.AddSingleton<GreetingService>();
        builder.AddGreetingCommands();

        using Galdr.Native.Galdr galdr = builder.Build().Run();
    }
}
```

`Run()` blocks on the native main loop until the window closes.

> **Match the dev-server port.** The `UrlContent(...)` URL must point at the port your
> front-end dev server actually serves. Vite, for example, defaults to **5173**, not the
> `5174` used here — if they don't match, the webview loads a blank page with no obvious
> error. Either change the URL to your server's port, or pin the server's port to the URL
> (Vite: `server: { port: 5174, strictPort: true }` in `vite.config.js`).

### Front-end build

The template already wires this up; the following is what it does, for manual setups.

Your web app builds to a `dist` folder. In release you copy that output next to the
executable (commonly as `wwwroot`) and point a content provider at it. With an MSBuild
`Content` item:

```xml
<ItemGroup>
  <Content Include="FrontEnd\dist\**\*.*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>wwwroot\%(RecursiveDir)%(Filename)%(Extension)</Link>
  </Content>
</ItemGroup>
```

If you use a bundler, set its base path to relative (e.g. Vite `base: './'`) so assets
resolve under `file://` / a virtual host.

---

## 3. Serving the UI — content providers

Pass one to `SetContentProvider(...)`. If you set none, Galdr defaults to
`LocalHostedContent` (serving `./dist`).

| Provider | Use it for | Notes |
|---|---|---|
| `UrlContent(url)` | Dev server during development | Hot reload; pair with `SetDebug(true)`. |
| `FolderContent(path, entry = "index.html", host = "app.local")` | Shipping a built SPA | No HTTP server. Uses a virtual host (Windows) or `file://` (macOS/Linux). Recommended for release. |
| `LocalHostedContent(port, ...)` | Serving a folder over loopback HTTP | Kestrel server on `127.0.0.1`. The default if no provider is set. |
| `EmbeddedContent(port, ns, assembly, dir)` | Bundling the UI as embedded resources | Served over loopback HTTP from the assembly. |

The common pattern is a debug/release split:

```csharp
#if DEBUG
    builder.SetDebug(true).SetContentProvider(new UrlContent("http://localhost:5174"));
#else
    builder.SetContentProvider(new FolderContent(Path.Combine(AppContext.BaseDirectory, "wwwroot")));
#endif
```

---

## 4. Dependency injection

Galdr has a built-in DI container (Microsoft.Extensions.DependencyInjection).

```csharp
builder.AddSingleton<Config>(config);           // instance
builder.AddSingleton<ILogger, FileLogger>();    // app-wide singleton
builder.AddService<NoteRepository>();           // transient (new per resolution)
builder.AddService<INoteService, NoteService>();
```

- **`AddSingleton`** — one instance for the app lifetime (config, connection managers, caches).
- **`AddService`** — transient; a fresh instance each time it's resolved (repositories, request-scoped work).

Keep your service registrations together in `Program.cs` — having them all in one place
is easy to scan and maintain.

Your services get normal constructor injection. To **use** a service, just add it as a
parameter to a command handler or resolve it from the `IServiceProvider` in a lifecycle hook.

These services are always registered and injectable:

| Type | Purpose |
|---|---|
| `Galdr` | The running app instance — window control, `Evaluate`, `Terminate`. |
| `IDialogService` / `DialogService` | Native file/folder dialogs. |
| `IEventService` / `EventService` | Push events to the front end. |
| `IGaldrJsonSerializer` | The AOT-safe JSON serializer. |

---

## 5. Commands — the JS to C# bridge

A command is a named C# handler the UI can call. Register them with:

- **`AddAction(name, handler)`** — fire-and-forget, no return value.
- **`AddFunction(name, handler)`** — returns a value (sync or `async Task<T>`).

```csharp
builder.AddFunction("getNote", (GetNoteRequest request, NoteService notes) =>
    notes.GetNote(request.Id));

builder.AddAction("closeWorld", (WorldService world) => world.CloseWorld());

builder.AddFunction("saveNote", async (SaveNoteRequest request, NoteService notes) =>
    await notes.SaveAsync(request));
```

### Recommended signature: object in, object out

**Take a single request object plus any DI parameters, and return an object.** This is the
smoothest, most reliable shape — it serializes cleanly and avoids the string-escaping
pitfalls that can show up when passing raw strings (especially strings that are themselves
JSON) as individual scalar arguments.

```csharp
// Request / response are plain classes.
internal class CreateNoteRequest { public string Name { get; set; } public int? ParentId { get; set; } }
internal class CreateNoteResponse { public int Id { get; set; } public bool Success { get; set; } }

builder.AddFunction("createNote", (CreateNoteRequest request, NoteService notes) =>
    notes.Create(request));   // returns CreateNoteResponse
```

Even single values are best wrapped in a small result object (e.g.
`class PathResult { public string Path { get; set; } }`) so the front end always receives
a consistent object.

Plain named scalar parameters also work (`(int id, string name, NoteService notes)`) and
are fine for one or two simple values, but prefer a request object as you add fields.

### Parameters and DI, mixed freely

Handler parameters are resolved two ways:

- A parameter whose type is a **registered service** is injected from DI and is **not** sent over the wire.
- Every other parameter is **deserialized from the call's argument object**.

So `(CreateNoteRequest request, NoteService notes)` deserializes `request` from the UI and
injects `notes`. Order doesn't matter for resolution; write whatever reads well.

### Serialization is automatic

GaldrJson's source generator inspects the parameter and return types of every `AddAction` /
`AddFunction` and registers them for AOT-safe (de)serialization. **You do not need to add
`[GaldrJsonSerializable]`** to your request/response classes (it's harmless if present).

- JSON property names are **camelCase** by default. Override a single property with
  `[GaldrJsonPropertyName("...")]`.
- Results returned to the UI come back camelCased.

### Calling from the front end

A global `galdrInvoke(name, argsObject)` is injected into the page. It returns a `Promise`
that resolves with the (camelCased) result, or rejects with `{ message }` if the handler throws.

The keys of `argsObject` are the **handler's parameter names** (DI parameters are omitted):

```js
// C#: (GetNoteRequest request, NoteService notes)   ->  request object nested under "request"
await galdrInvoke('getNote', { request: { id: 5 } });

// C#: (int id, string name, NoteService notes)      ->  flat scalars by parameter name
await galdrInvoke('updateNoteName', { id: 5, name: 'Intro' });

// C#: (NoteService notes) only                       ->  no args
await galdrInvoke('getNotes');
```

A thin wrapper plus per-feature modules keeps the UI tidy and centralizes error handling:

```js
// invoke.js
export async function invoke(command, args) {
  try {
    return args === undefined ? await galdrInvoke(command) : await galdrInvoke(command, args);
  } catch (err) {
    showErrorToast(err?.message);
    throw err;
  }
}

// noteService.js
import { invoke } from './invoke';
export const getNote   = (id)      => invoke('getNote',   { request: { id } });
export const saveNote  = (request) => invoke('saveNote',  { request });
```

### Organizing command registration

Group commands by feature into `GaldrBuilder` extension methods — one static class per
feature — and chain them in `Program.cs`. This keeps `Program.cs` from sprawling as the
app grows.

```csharp
internal static class NoteCommands
{
    public static GaldrBuilder AddNoteCommands(this GaldrBuilder builder)
    {
        builder.AddFunction("getNotes",  (NoteService notes) => notes.GetAll());
        builder.AddFunction("getNote",   (GetNoteRequest request, NoteService notes) => notes.Get(request.Id));
        builder.AddFunction("createNote",(CreateNoteRequest request, NoteService notes) => notes.Create(request));
        return builder;
    }
}

// Program.cs
builder.AddNoteCommands()
       .AddWorldCommands()
       .AddExportCommands();
```

---

## 6. Events — pushing from C# to the UI

Inject `IEventService` and call `PublishEvent`. The `args` string is raw JSON that becomes
the event's `detail`; the event is dispatched on the UI thread.

```csharp
public class NoteService
{
    private readonly IEventService _events;
    public NoteService(IEventService events) => _events = events;

    public void Save(Note note)
    {
        // ...persist...
        _events.PublishEvent("noteSaved", $"{{ \"id\": {note.Id} }}");
    }
}
```

On the front end:

```js
window.addEventListener('noteSaved', (e) => {
  console.log('saved note', e.detail.id);
});
```

For events with no payload you can also dispatch directly from the C# side via the `Galdr`
instance: `galdr.Evaluate("window.dispatchEvent(new Event('before-close'))")`.

---

## 7. Native dialogs

Inject `IDialogService` (or `DialogService`). Filter lists are comma-separated extensions.

```csharp
builder.AddFunction("pickImage", (DialogService dialogs, ImageService images) =>
{
    string path = dialogs.OpenFileDialog("png,jpg,jpeg,webp", defaultPath: null);
    return new PathResult { Path = path };  // path is null if the user cancelled
});
```

| Method | Returns |
|---|---|
| `OpenFileDialog(filterList?, defaultPath?)` | selected file path, or `null` |
| `OpenFileDialogMultiple(filterList?, defaultPath?)` | array of paths, or `null` |
| `OpenSaveDialog(filterList?, defaultPath?, defaultName?)` | chosen save path, or `null` |
| `OpenDirectoryDialog(defaultPath?)` | selected folder path, or `null` |

---

## 8. Window control

The `Galdr` instance (injectable, or returned from `Run()`) controls the window:

| Member | Description |
|---|---|
| `SetTitle(title)` | Update the window title. |
| `SetSize(w, h, WebviewHint)` | Set size; `None` (default), `Min`, `Max`, or `Fixed` bounds. |
| `SetPosition(x, y)` | Move the window (top-left screen coords). No-op on Wayland. |
| `SetWindowState(WindowState)` | `Normal` / `Minimized` / `Maximized` / `Fullscreen`. |
| `GetSize()` / `GetPosition()` / `GetWindowState()` | Read current geometry/state. |
| `Evaluate(js)` | Run JavaScript in the page (result ignored). |
| `Dispatch(action)` | Run an action on the UI thread. |
| `Terminate()` | Close the window and stop the main loop. |
| `GetWindow()` / `GetNativeHandle(kind)` | Native handles for advanced interop. |

> Platform caveats: under Wayland, absolute window position can't be set or read
> (`GetPosition()` returns `null`), and a minimized window may report as `Normal`. On
> macOS, `Maximized` maps to AppKit "zoom". These are documented on the relevant types.

---

## 9. Application lifecycle & builder options

### Builder configuration

| Method | Effect |
|---|---|
| `SetTitle(title)` | Window title. |
| `SetSize(w, h)` / `SetMinSize(w, h)` | Default and minimum window size. |
| `SetPort(port)` | Port for hosted content providers. |
| `SetDebug(true)` | Enable webview dev tools. |
| `SetContentProvider(provider)` | Choose how the UI is served (section 3). |
| `SetLoadingPage(message?, color?)` | Show a spinner page until the main content is ready. |
| `EnableSpellChecking("en_US", ...)` | Enable spell check (Linux/macOS). |
| `SetInitScript(js)` | JS injected before every page load. |
| `UseSingleInstance(appId)` | Enforce a single running instance (see below). |

### Lifecycle hooks

| Hook | When it runs |
|---|---|
| `OnBeforeStartup(Action)` | Before the webview/services exist — DB migrations, directory prep. |
| `OnStartup(Action<IServiceProvider>)` | After services are built and window exists, before the loop. |
| `OnAfterStartup(Action<IServiceProvider>)` | Dispatched on the UI thread once the loop is running. |
| `OnBeforeClose(Action<Galdr>)` | User tried to close — the close is **cancelled**; you call `Terminate()` when ready. |
| `OnSecondInstance(Action<string[], string>)` | A duplicate launch was detected (with single-instance). |
| `OnCommandError(Action<CommandErrorContext, IServiceProvider>)` | A command handler threw (error already returned to UI). |
| `OnUnhandledException(Action<UnhandledExceptionContext, IServiceProvider>)` | Escaped exceptions / faulted tasks. |
| `OnWindowChanged(Action<Galdr, WindowChangedContext, IServiceProvider>)` | Debounced (~250 ms) resize/move/state change. |

### Graceful close (async save before exit)

`OnBeforeClose` cancels the close automatically, giving the UI a chance to save:

```csharp
builder.OnBeforeClose(galdr =>
{
    galdr.Evaluate("window.dispatchEvent(new Event('before-close'))");
});

// And a command the UI calls once it has finished saving:
builder.AddAction("confirmClose", (Galdr galdr) => galdr.Terminate());
```

```js
window.addEventListener('before-close', async () => {
  await saveEverything();
  await galdrInvoke('confirmClose');   // now the app actually exits
});
```

### Persisting window geometry

```csharp
builder.OnWindowChanged((galdr, ctx, sp) =>
{
    if (ctx.State == WindowState.Normal)
        sp.GetRequiredService<SettingsService>().SaveWindow(ctx.Width, ctx.Height, ctx.State);
});
```

### Single instance

```csharp
builder.UseSingleInstance("myapp")
       .OnSecondInstance((args, cwd) => { /* focus happens automatically; handle args */ });
```

A duplicate launch focuses the primary window, fires `OnSecondInstance`, and exits without
creating a second window.

### Error logging

```csharp
builder.OnCommandError((ctx, sp) =>
    sp.GetService<ILogger>()?.Error(ctx.CommandName, "Command failed", ctx.Exception));
```

---

## 10. External links

External `http`/`https` links clicked in the UI are intercepted automatically and opened in
the user's default browser (not navigated inside the webview). No configuration required.

---

## 11. Packaging notes

- The Galdr.Native package bundles the native webview and dialog libraries under
  `runtimes/<rid>/native`; they're copied to your output automatically.
- Ship your built front end alongside the executable (section 2) and point a `FolderContent`
  (or other provider) at it in release.
- AOT is supported but optional — enable `PublishAot` only if your full dependency set is
  AOT-compatible. The framework and its serialization work either way.

---

## Quick reference

```csharp
// Build & run
using Galdr.Native.Galdr galdr = new GaldrBuilder()
    .SetTitle("My App")
    .SetSize(1280, 800)
    .SetMinSize(800, 600)
    .SetContentProvider(new FolderContent(wwwroot))   // or UrlContent in debug
    .AddSingleton<Config>(config)
    .AddService<NoteService>()
    .AddNoteCommands()                                 // your extension methods
    .Build()
    .Run();

// Command: object in (+ DI), object out
builder.AddFunction("createNote", (CreateNoteRequest req, NoteService notes) => notes.Create(req));

// Front end
const res = await galdrInvoke('createNote', { request: { name: 'Intro' } });
window.addEventListener('noteSaved', e => console.log(e.detail));
```
