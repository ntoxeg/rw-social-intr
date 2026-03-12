# Environment

**What belongs here:** Build tools, SDK versions, dependency quirks, platform notes.
**What does NOT belong here:** Service ports/commands (use `.factory/services.yaml`).

---

- **OS:** Windows 10
- **dotnet SDK:** 9.0.312
- **.NET Framework:** 4.8 (targeting pack available)
- **Target Framework:** net48
- **Build:** `dotnet build SocialInteractions\SocialInteractions.csproj -c Release`
- **NuGet packages:** Krafs.Rimworld.Ref 1.6.4633 (RimWorld ref assemblies), Lib.Harmony 2.3.3
- **Local reference:** Assembly-CSharp.dll at repo root (game's main assembly)
- **Output:** SocialInteractions\1.5\Assemblies\SocialInteractions.dll
- **.csproj uses EnableDefaultCompileItems=true** — auto-discovers .cs files in subdirectories
- **.csproj has Compile Remove rules:** `1.5\**`, `Defs\**`, `Languages\**`, `About\**` — code directory MUST NOT be named `Defs/`
