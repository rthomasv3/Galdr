## Galdr

Galdr is a framework for building multi-platform desktop applications using C#. It's powered by [webview](https://github.com/webview/webview) and compatible with any frontend web framework of your choice.

Features:
* Cross-platform (Windows, Linux, macOS)
* Call C# methods asynchronously from javascript/typescript
* Compatible with any frontend framework (Vue, React, etc.)
* Hot-reload
* Native file system integration
* Single file executable
* Small deliverable binary size (as low as 6.8MB compressed)
* Dependency injection

![POC Screenshot](https://raw.githubusercontent.com/rthomasv3/Galdr/master/Galdr/screenshot.png)

### Setup

The setup is pretty straight forward and steps are outlined below - or you can use the proof-of-concept example project [here](https://github.com/rthomasv3/GaldrPOC) as a template.

1. Create a new C# console application.
2. Use `<OutputType>WinExe</OutputType>` instead of `Exe` to make sure the console window is hidden.
3. Setup your frontend app in a `src` directory inside your C# project.
4. Create your `index.html` and `package.json` just like you normally would when setting up a front end project.
5. Add the `dist` directory path to your `csrpoj` so it will be included as an emeded resource.
6. Setup Galdr in `Main`.
    * The entry point to the application requires the `[STAThread]` attribute on Windows.
7. Optionally set the project as the trim root assembly for smaller binaries (required when trimming due to reflection).

### Linux Prerequisites

Linux support requires `webkit2gtk` and `gtk3`.

With a distribution using apt:
```
sudo apt install -y libwebkit2gtk-4.1-dev libgtk-3-dev
```

With a distribution using dnf:
```
sudo dnf install webkit2gtk4.1-devel gtk3-devel
```

#### Example Main

```cs
internal class Program
{
    // Single-threaded apartment is required for COM on Windows
    [STAThread]
    static void Main(string[] args)
    {
        using Galdr.Galdr galdr = new GaldrBuilder()
            .SetTitle("Galdr + C# + Vue 3 App")
            .SetSize(1024, 768)
            .SetMinSize(800, 600)
            .AddSingleton<SingletonExample>()
            .AddService<TransientExample>()
            .SetPort(1313)
            .Build()
            .Run();
    }
}
```

The front end should be included as an embedded resource.

```xml
<ItemGroup>
  <EmbeddedResource Include="dist\**\*.*">
    <CopyToOutputDirectory>Never</CopyToOutputDirectory>
  </EmbeddedResource>
</ItemGroup>
```

Any method tagged with the `[Command]` attribute in the `Command` namespace will be added for use on the frontend. The namespace can be customized by calling `SetCommandNamespace` on the builder. The command attribute optionally takes in a command name (it uses the method name by default).

```cs
[Command]
public static string Greet(string name)
{
    return $"Hello, {name}! You've been greeted from C#!";
}
```

Then you can use the command anywhere on the frontend with `galdrInvoke`. The command names are made camelCase in `js`.

```js
galdrInvoke("greet", { name: name.value })
    .then(name => greetMsg.value = name)
    .catch(e => console.error(e));

// or using async/await

greetMsg.value = await galdrInvoke("greet", { name: name.value });
```

Any additional parameters can be added to the `galdrInvoke` call after the command name. The parameters will automatically be deserialized and passed into the C# method. Parameters not passed in by the frontend will be evaluated via dependency injection. The command's class can also contain dependencies in the constructor.


## Debugging

To debug the application with hot-reload, open a terminal and start the server.

```
npm install
npm run dev
```

Then just hit `F5` and you can start and debug the application like normal.


## Building

The frontend is served from files embedded into the assembly in the `dist` directory on build, so the first step is to build the frontend.

```
npm install
npm run build
```

Then you can build the app as a single file using `dotnet publish` - just be sure to update to the platform.

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

### Trimming

To reduce the binary size you can optionally trim the code.

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=True -p:TrimMode=link
```

Note that trimming can break reflection when classes aren't statically referenced. This can be fixed by setting the trimmer root assembly in the `csproj`.

```xml
 <ItemGroup>
   <TrimmerRootAssembly Include="$(AssemblyName)" />
 </ItemGroup>
```

## Galdr.Native

`Galdr.Native` is designed for native AOT support. It has all the same features as `Galdr` with a couple small differences:

Additional Features:
* Full AOT support
* Automatic source generation for command request and response JSON serialization

Commands are no longer added using Attributes or a namespace due to AOT constraints. Instead commands are added using the `AddAction` and `AddFunction` methods on `GaldrBuilder`.

Actions and functions support up to 16 parameters. There are no changes to the way commands are called from the frontend code.

### Native Command Examples

```csharp
// Actions are used for void return types
builder.AddAction("commandTest1", () =>
{
    Debug.WriteLine("Command Test 1!");
});

// Functions are used when you need to return something from the command
builder.AddFunction("commandTest3", (int x) =>
{
    Debug.WriteLine($"Command Test 3 {x}!");

    return new TestResult { };
});

// Dependency injection is fully supported
builder.AddSingleton<PrintService>();
builder.AddFunction("commandTest4", (int count, PrintRequest request, PrintService printService) =>
{
    for (int i = 0; i < count; ++i)
    {
        printService.Print(request.Id, request.Name);
    }

    return new PrintResponse
    {
        Success = true,
    };
});
```

Any classes that appear as a return type or parameters for actions/functions are detected on build. Source generation is used to create the needed JSON serializers for these classes. This was designed to be simple and automatic - there is no need for a `JsonSerializerContext` like with minimal APIs.
