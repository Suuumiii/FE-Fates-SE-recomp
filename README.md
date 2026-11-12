# Fire Emblem Fates: Special Edition Recomp

<a href="https://recomplauncher.com"><picture><source media="(prefers-color-scheme: dark)" srcset="https://recomplauncher.com/assets/press/launcher/badge-light.svg"><img alt="Get it on RecompLauncher" src="https://recomplauncher.com/assets/press/launcher/badge-dark.svg" height="48"></picture></a>

A Windows launcher for building and playing the Zakuro recompilation of the European *Fire Emblem Fates: Special Edition*. You provide your own decrypted game image; the launcher builds the PC files locally.

## What you need

- A decrypted European *Fire Emblem Fates: Special Edition* game image in `.3ds`, `.cci` or `.cxi` format. Encrypted images are not supported.
- The `3dsrecomp` and Zakuro tools. They are included in the ready-to-use release ZIP under `tools\3dsrecomp` and `tools\zakuro`; they are not included in this source repository.
- Windows 10 or later. On the first build, the launcher can download Zig 0.17.1 (about 96 MB) after asking you, or use a compatible GCC already on your `PATH`.
- Internet access if you want the launcher to download the compiler or one of the recommended mods.

## How to play

1. Download and extract the latest Windows ZIP from [GitHub Releases](https://github.com/Suuumiii/FE-Fates-SE-recomp/releases/latest).
2. Open **Fates Launcher.exe**.
3. In **Game Files**, choose your decrypted game image and an output folder for the generated PC files.
4. Select **Build PC Files**. Allow the Zig download if you do not have a compatible GCC installed. The first build can take several minutes.
5. Choose your renderer, rasterizer, screen layout and window scale, then select **Launch Game**.

The launcher saves its settings beside the executable. You can also install and configure the launcher from RecompLauncher; its release profile is included at the top of the ZIP. RecompLauncher opens Fates Launcher with the selected game image filled in, and the normal build and launch flow still happens in Fates Launcher.

## Features

- Build the game locally from your decrypted image with `3dsrecomp` and Zakuro.
- Choose Vulkan, OpenGL or software rendering; hardware or software 3D rasterization; top-only, bottom-only, stacked, side-by-side or large-top screen layouts; and window scale.
- Configure keyboard and controller bindings in the launcher.
- Download and apply supported mods from the Mods tab. Mod archives are checksum-checked; installation steps differ by mod.
- Save and restore launcher settings, and keep the game image and generated PC files in folders you choose.

## Recommended mods

Downloads come from each mod's GameBanana page. The launcher verifies downloaded archives. Some mods install directly into Zakuro after you build the game; others require the creator's manual extraction and rebuild instructions.

### Fire Emblem Fates Texture Compilation 6.7

[GameBanana page](https://gamebanana.com/mods/388979) · Uploader and compiler: **YiannisG**. The page credits contributors for textures, ports, models, animations, tools and playtesting, including Algo112233, AzureFEF, Brian_alien, DeathChaosV2, L498, Thane98, TildeHat and others. See the mod page for the full, current credits. This archive requires manual extraction and a game rebuild; follow the instructions on its page.

### Unofficial Gay Fates

[GameBanana page](https://gamebanana.com/mods/51420) · Uploaded by **Milfnuggy**. The current development team is **dingleflongle, flutea and Cherrycrow/Sunny**. The page also credits the retired team, special thanks, and support writers. The launcher installs it for the title ID detected in your generated game files.

### Fates UI Remastered

[GameBanana page](https://gamebanana.com/mods/542115) · Created by **Korum**, with **L498** credited for various UI elements. The creator also credits Aoryn and FEPortraits, and assets from Fates Icon Project and Fates HD Additions. See the included `Asset References.txt` and the GameBanana page for full attribution. The launcher installs it as a Zakuro texture pack for the detected title ID.

## Build

### Build the launcher from source

Install the .NET 10 SDK on Windows, clone this repository, and run:

```powershell
dotnet publish .\launcher-src\FatesLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\launcher-build
```

The launcher executable will be `launcher-build\FatesLauncher.exe`. To package a release, place `3dsrecomp.exe` in `release-tools\3dsrecomp` and `zakuro.exe` in `release-tools\zakuro`, then run:

```powershell
.\scripts\package-release.ps1 `
  -LauncherExe .\launcher-build\FatesLauncher.exe `
  -ToolsDirectory .\release-tools `
  -OutputZip .\dist\FE-Fates-SE-Recomp-Windows-x64.zip
```

The packaging script includes the launcher, required tools, notices, README and RecompLauncher profile. It excludes game images, dump metadata, generated game code and compiler bundles. To rebuild the PC game files, use the launcher with your own dump and the tools above; the source repository does not include those executables.

## Legal

You need your own dump of the game. The project has no decryption and no keys, and Zakuro refuses a dump that is still encrypted.

The repository and the launcher contain no code, data or assets from the game. What they hold about it, such as addresses in its code and the names of its files, is listed in [LEGAL.md](LEGAL.md). What the launcher builds is made from your copy and stays on your computer: the converted code in `%APPDATA%\3dsrecomp` and the mods in `%APPDATA%\zakuro\mods` on Windows, or `~/.local/share/3dsrecomp` and `~/.local/share/zakuro/mods` on Linux. Don't share either.

The project's own files are under the MIT license ([LICENSE](LICENSE)). Zakuro and 3dsrecomp keep their own licenses ([THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt)). Fire Emblem is a trademark of Nintendo. This project is not affiliated with Nintendo or Intelligent Systems.
