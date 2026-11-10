# @nexusmods/fomod-installer-ipc

A Node.js package for installing [FOMOD](https://fomod-docs.readthedocs.io/) mod archives. It spawns a .NET process and communicates with it over IPC (stdin/stdout), giving you access to the full FOMOD feature set -- including both XML and C# install scripts.

This is the integration used by [Vortex](https://www.nexusmods.com/about/vortex/), the Nexus Mods mod manager.

## When to use this vs `@nexusmods/fomod-installer-native`

| | `fomod-installer-ipc` | `fomod-installer-native` |
|---|---|---|
| Script support | XML + C# | XML only |
| Runtime | Requires .NET 9 | No runtime needed (Native AOT) |
| Integration | Spawns a child process | Loads a shared library via N-API |
| Platforms | Windows (C# scripts), cross-platform (XML only) | Windows, Linux |

Use this package if you need C# script support. Use `@nexusmods/fomod-installer-native` if you only need XML scripts and want a lighter footprint.

## Requirements

- Node.js 22+
- .NET 9 runtime (bundled during build)

## License

[GPL-3.0](https://github.com/Nexus-Mods/fomod-installer/blob/master/LICENSE.md)
