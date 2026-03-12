# Testing Constraints for RimWorld Mod

## Unity Reference Assembly Limitation

The `Krafs.RimWorld.Ref` NuGet package provides **compile-time reference assemblies only**. These assemblies allow the code to compile against RimWorld/Unity types but do **not** include runtime implementations.

**Consequence:** Any test that instantiates a type touching `UnityEngine` or `Verse` types will fail at runtime with `FileNotFoundException` for `UnityEngine.CoreModule.dll`.

**Workaround:** Use source-level characterization tests (regex-based assertions on `.cs` file content) instead of runtime instantiation for testing mod types that depend on Unity/RimWorld assemblies. Only pure-data types (enums like `LlmApiType`, simple POCOs) can be instantiated in tests.

## Assembly-CSharp Copy-to-Output

The test project needs `Assembly-CSharp.dll` copied to the test output directory via `<None CopyToOutputDirectory>` in the test `.csproj`. Without this, tests fail at runtime with `FileNotFoundException` even for types that don't directly use game types (transitive dependency loading).

## Testable Code Surface

Approximately 5-15% of the codebase is unit-testable without the game runtime:
- Enum values and constants
- Source-level structural assertions (file exists, contains expected patterns)
- Pure utility functions that don't reference Verse/RimWorld types
- API request/response JSON construction (via source-level inspection)
