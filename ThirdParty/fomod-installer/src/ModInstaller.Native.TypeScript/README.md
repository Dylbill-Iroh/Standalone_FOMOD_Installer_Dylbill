# @nexusmods/fomod-installer-native

A Node.js package for installing [FOMOD](https://fomod-docs.readthedocs.io/) mod archives. It loads a Native AOT compiled shared library via N-API bindings -- no .NET runtime required. Supports XML-based install scripts.

This is part of the [fomod-installer](https://github.com/Nexus-Mods/fomod-installer) project by Nexus Mods.

## When to use this vs `@nexusmods/fomod-installer-ipc`

| | `fomod-installer-native` | `fomod-installer-ipc` |
|---|---|---|
| Script support | XML only | XML + C# |
| Runtime | No runtime needed (Native AOT) | Requires .NET 9 |
| Integration | Loads a shared library via N-API | Spawns a child process |
| Platforms | Windows, Linux | Windows (C# scripts), cross-platform (XML only) |

Use this package for a lightweight integration when you only need XML scripts. Use `@nexusmods/fomod-installer-ipc` if you also need C# script support.

## Requirements

- Node.js 22+
- N-API version 8+

## License

[GPL-3.0](https://github.com/Nexus-Mods/fomod-installer/blob/master/LICENSE.md)
