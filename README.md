# Fire Emblem Fates: Special Edition Recomp Launcher

<a href="https://recomplauncher.com"><picture><source media="(prefers-color-scheme: dark)" srcset="https://recomplauncher.com/assets/press/launcher/badge-light.svg"><img alt="Get it on RecompLauncher" src="https://recomplauncher.com/assets/press/launcher/badge-dark.svg" height="48"></picture></a>

Windows launcher source for configuring and launching the Zakuro recompilation, with configurable keyboard/controller bindings and mod management.

## RecompLauncher

The release ZIP includes `recomplauncher.json` at its top level. In RecompLauncher, select your own decrypted European Special Edition `.3ds`, `.cci` or `.cxi` image, then start **Fates Launcher.exe**. The launcher keeps its normal build and launch workflow; build the PC files from your image before launching the game. No game image, generated game code, or dump metadata is distributed. The first build may offer to download the pinned Zig compiler if no compatible GCC is installed.

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
