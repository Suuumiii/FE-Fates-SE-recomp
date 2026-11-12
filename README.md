# Fire Emblem Fates: Special Edition Recomp Launcher

Windows launcher source for configuring and launching the Zakuro recompilation, with configurable keyboard/controller bindings and mod management.

## Build

Install the .NET 10 SDK on Windows, then run:

```powershell
dotnet publish .\launcher-src\FatesLauncher.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\launcher-build
```

## Local game and tool files

This repository intentionally does not contain a game dump, game metadata, generated recompiled code, or bundled tool binaries. Use a legally obtained, decrypted game image and choose its location and an output folder in the launcher.

For build and launch features, place compatible tools beside the launcher in these paths:

- `tools\3dsrecomp\3dsrecomp.exe`
- `tools\mingw64\bin\gcc.exe` (with its required runtime files)
- `tools\zakuro\zakuro.exe`

The generated recompiled code belongs in the output folder selected in the launcher and should remain local.
