# Fire Emblem Fates: Special Edition Recomp Launcher

Windows launcher source for configuring and launching the Zakuro recompilation, with configurable keyboard/controller bindings and mod management.

## Build

Install the .NET 10 SDK on Windows, then run:

```powershell
dotnet publish .\launcher-src\FatesLauncher.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\launcher-build
```

## Local game and tool files

This repository intentionally does not contain a game dump, game metadata, generated recompiled code, or bundled tool binaries. Use your own decrypted game image and choose its location and an output folder in the launcher.

Place compatible runtime tools beside the launcher:

- `tools\3dsrecomp\3dsrecomp.exe`
- `tools\zakuro\zakuro.exe`

The launcher downloads Zig 0.17.1 from ziglang.org on the first build, verifies its published SHA-256 checksum, and caches it under `%LOCALAPPDATA%\FE-Fates-SE-Recomp\compiler`. Internet access is required for that one-time compiler setup. An installed GCC on the system PATH is also detected and can be used instead.

Generated recompiled code belongs in the output folder selected in the launcher and should remain local.