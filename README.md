# Standalone FOMOD Installer

A small Windows program that runs a mod's FOMOD installer without a mod manager, then copies the chosen files to destination folder(s) you choose.
Official Nexus Mods Page: [Standalone FOMOD Installer](https://www.nexusmods.com/site/mods/2578)

# What It Does

1. Choose a mod archive (`.zip`, `.7z`, `.rar`) or an extracted mod folder. You can also drag the archive onto the window, or onto the `.exe`.
2. Optionally choose the game's data folder. Some FOMODs show or hide options depending on which plugins or files you already have (for example "install the USSEP patch if USSEP is active"). Without it, every plugin counts as not installed. **Detect Game Data Folder** reads the mod's installer and tells you whether it needs one, which game it's for, which folder that is (e.g. `Data` for Skyrim, the game's main folder for Cyberpunk 2077), and where that game is installed (Steam, GOG, Epic or the registry).
3. Choose the folder to install to. Click **+ Add folder** to install the same files to more than one folder (for example two game installs). The folders are filled in again next time.
4. Click **Start**, go through the installer's pages, then click **Install**.

Paths are remembered in `%APPDATA%\StandaloneFomodInstaller\settings.json`. Active plugins are read from the plugins.txt of the game the data folder belongs to (for example `%LOCALAPPDATA%\Skyrim Special Edition\plugins.txt`). To use a different file, set `PluginsTxtPath` in settings.json.

Known games are listed in `Games.xml` next to the `.exe`: Skyrim SE/AE, Skyrim VR, Skyrim (2011), Fallout 4, Fallout 4 VR, Fallout: New Vegas, Fallout 3, Oblivion, Morrowind, Starfield, Cyberpunk 2077. Edit it to add or change games (the format is explained at the top of the file) and restart the program. If the file is missing or broken, the built-in copy is used and "Detect Game Data Folder" says so.

## Build

Needs the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (or newer) on Windows.

- `build.bat`: a normal build, with the program's files separate in the `publish` folder. Users need the .NET 9 Desktop Runtime to run it.
- `build_full.bat`: a single `.exe` with .NET built in, so users don't need to install anything.

Either way the program is `publish\StandaloneFomodInstaller.exe`, with `publish\Games.xml` next to it (keep them together).

## What's supported

- XML FOMOD installers (`fomod\ModuleConfig.xml`, schema 1.0 to 5.0), which is almost all of them.
- Not supported: C# script installers (`script.cs`), INI edits, and enabling plugins in your load order. These are listed as notes before you install.
- Mods without a FOMOD can be installed as-is: the files are copied with the archive's top folder stripped (e.g. `MyMod\Data\textures\...` installs as `textures\...`).

## Credits / license

Standalone FOMOD Installer, Copyright (C) 2026 Dylbill.
Licensed under the GNU General Public License v3.0, see the LICENSE file.
Source code: https://github.com/Dylbill-Iroh/Standalone_FOMOD_Installer_Dylbill

Built with assistance from [Claude AI](https://anthropic.com) by Anthropic.

This program includes:

- Nexus Mods' fomod-installer (https://github.com/Nexus-Mods/fomod-installer), GPL-3.0.
  Copyright (C) Black Tree Gaming Ltd. and contributors.
- SharpCompress (https://github.com/adamhathcock/sharpcompress), MIT license.
  Copyright (c) 2014 Adam Hathcock.
- The .NET runtime (https://github.com/dotnet/runtime), MIT license.
  Copyright (c) .NET Foundation and Contributors.
  Its third-party notices: https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT

MIT license text (applies to SharpCompress and .NET):

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
