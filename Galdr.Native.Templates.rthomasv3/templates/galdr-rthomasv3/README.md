# GaldrApp

A [Galdr.Native](https://www.nuget.org/packages/Galdr.Native) desktop app: Vue 3
(Composition API, JavaScript) + Tailwind CSS v4 + auto-imported Lucide icons,
with system/light/dark theming and file logging. Targets .NET 10 with AOT.

## Prerequisites

- .NET 10 SDK
- Node.js 20.19+ or 22.12+ (required by Vite 8)

## Run

**Debug (hot reload):**

```sh
cd FrontEnd
npm install
npm run dev
```

Then, in another shell:

```sh
dotnet run -c Debug
```

Debug serves the UI from the Vite dev server at `http://localhost:5174`.

**Release (one shot):**

```sh
dotnet run -c Release
```

The Release build compiles the front end and stages it into `wwwroot`, which the
app serves from disk. No separate front-end step is needed.

## Layout

```
GaldrApp.csproj      App project (net10.0, AOT, front-end build wired in)
Program.cs           GaldrBuilder setup, error/exception logging hooks
Config.cs            Resolves the per-user app-data paths (log file)
Services/            C# services (ILoggingService + FileLoggingService)
FrontEnd/            Vue 3 + Vite + Tailwind front end
  src/
    services/        Thin wrappers over the galdrInvoke bridge (invoke.js)
    stores/          Pinia stores (themeStore.js)
    utils/           Helpers (platform.js)
    views/           Route components (DashboardView.vue)
    router.js        vue-router (hash history)
    style.css        Tailwind + theme tokens (light/dark)
```

## Notes

- **Icons:** use any Lucide icon as a component on demand, e.g.
  `<i-lucide-sparkles />`. Add more `@iconify-json/*` collections to use other sets.
- **Theme:** `themeStore` follows the OS by default and persists a manual override
  to `localStorage`, toggling a `light`/`dark` class on `<html>`.
- **Logging:** `FileLoggingService` writes to `log.txt` in the per-user app-data
  directory; command and unhandled exceptions are logged automatically.
