# Building SocialInteractions

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (or later)

That's it. No RimWorld install is needed to compile — the project uses NuGet reference assemblies.

## Quick Start

```bash
cd SocialInteractions
dotnet build
```

The compiled DLL will be placed in `1.5/Assemblies/SocialInteractions.dll`.

## How It Works

The project uses a standard `.csproj` MSBuild file with two key NuGet packages:

- **[Krafs.Rimworld.Ref](https://github.com/krafs/RimRef)** (v1.6.4633) — provides RimWorld and Unity reference assemblies stripped of code, so the compiler has all the type signatures it needs without requiring a game install. Published with permission from Ludeon Studios.
- **[Lib.Harmony](https://www.nuget.org/packages/Lib.Harmony)** (v2.3.3) — the Harmony patching library used for runtime method patching.

The repo's own `Assembly-CSharp.dll` (in the repository root) is referenced directly for any game types not covered by the NuGet stubs.

## Build Configuration

| Setting | Value |
|---|---|
| Target framework | .NET Framework 4.8 (`net48`) |
| Output path | `1.5/Assemblies/` |
| Assembly name | `SocialInteractions` |

## Migration from compile.bat

The old build setup used `compile.bat` + `compile.rsp` which called `csc.exe` directly with hardcoded paths to a specific Windows machine's RimWorld install. The new `.csproj` replaces both of those files and is:

- **Portable** — builds on Windows, Linux, and macOS without a game install
- **IDE-friendly** — works with Visual Studio, Rider, and VS Code out of the box
- **Dependency-managed** — NuGet handles all external references automatically
