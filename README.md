﻿## Galdr

Galdr is a WIP framework for building multi-platform desktop applications using C#. It's powered by [webview](https://github.com/webview/webview) and compatible with any frontend web framework of your choice.

Features:
* Cross-platform (Windows, Linux, macOS)
* Call C# methods asynchronously from javascript/typescript
* Compatible with any frontend framework (Vue, React, etc.)
* Hot-reload
* Native file system integration
* Single file executable
* Reasonable binary size (example is 23.7MB)
* Dependency injection

![screenshot](https://raw.githubusercontent.com/rthomasv3/Galdr/master/Galdr/screenshot.png)

### Setup

The setup is pretty straight forward:

1. Create a new C# console application.
2. Setup your frontend app in a `src` directory inside your C# project.
3. Create your `index.html` and `package.json` just like you normally would when setting up a front end project.
4. Add the `dist` directory path to your `csrpoj` so it will be included as an emeded resource.
5. Setup Galdr in `Main`.
6. Optionally set the project as the trim root assembly for smaller binaries (required when trimming due to reflection).

> Use `<OutputType>WinExe</OutputType>` instead of `Exe` to make sure the console window is hidden.

An example project can be found [here](https://github.com/rthomasv3/GaldrPOC) to use as a template.

#### Example Main

> Please note the `[STAThread]` attribute is required.

```cs
internal class Program
{
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
            .Build();

        galdr.Run();
    }
}
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
    .then(a => console.log(a))
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
   <TrimmerRootAssembly Include="YouProjectNameHere" />
 </ItemGroup>
```
