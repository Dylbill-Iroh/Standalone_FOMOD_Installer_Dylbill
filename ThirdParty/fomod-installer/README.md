# FOMOD Installer

A library for processing [FOMOD](https://fomod-docs.readthedocs.io/) mod archives, the standard format used by game modding communities to package and install mods with user-configurable options. Built primarily for [Vortex](https://www.nexusmods.com/about/vortex/), the Nexus Mods mod manager.

## How it works

The installer reads a FOMOD archive, parses its install script (XML-based or C#-based), walks the user through any configuration steps the mod author defined, and produces a list of files to install.

There are two ways to consume it from Node.js:

- **IPC** (`@nexusmods/fomod-installer-ipc`) -- spawns a .NET process and communicates over stdin/stdout. Supports the full feature set including C# scripts (Windows only).
- **Native** (`@nexusmods/fomod-installer-native`) -- loads a Native AOT compiled shared library via N-API. Lighter weight, no .NET runtime required, but limited to XML scripts.

## Project structure

```
src/
  FomodInstaller.Interface/   Core interfaces and data types
  Utils/                      Shared utilities
  AntlrUtil/                  ANTLR 3 parser helpers

  InstallScripting/
    Scripting/                Base scripting abstractions
    XmlScript/                XML-based FOMOD script interpreter (versions 1.0-5.0)
    CSharpScript/             C# script execution (Windows only)

  ModInstaller.Adaptor.Dynamic/   Full adapter with all script types
  ModInstaller.Adaptor.Typed/     Lightweight adapter (XML scripts only)

  ModInstaller.IPC/               .NET executable for IPC-based integration
  ModInstaller.IPC.TypeScript/    npm package wrapping the IPC executable

  ModInstaller.Native/            Native AOT shared library (win-x64, linux-x64)
  ModInstaller.Native.TypeScript/ npm package with N-API bindings to the native library

test/                         Test projects and shared test data
```

## Requirements

- .NET 9 SDK
- Node.js 22+
- pnpm

## License

[GPL-3.0](LICENSE.md)

