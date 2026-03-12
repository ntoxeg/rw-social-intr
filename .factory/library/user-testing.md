# User Testing Knowledge

## Testing Surface

This is a RimWorld mod — there is **no interactive UI testing surface** available. The game cannot be launched headlessly. All validation is done through:

1. **Build verification**: `dotnet build SocialInteractions\SocialInteractions.csproj -c Release` — must succeed with 0 errors
2. **Unit tests**: `dotnet test SocialInteractions.Tests\SocialInteractions.Tests.csproj --no-restore` — all tests must pass
3. **Code inspection**: `rg` (ripgrep) for structural assertions (interface existence, class hierarchy, namespace declarations, etc.)

## No Services Required

No external services, databases, or running processes are needed. Build and test commands run entirely locally using `dotnet SDK 9.0.312` targeting `.NET Framework 4.8`.

## Test Project

- Path: `SocialInteractions.Tests\SocialInteractions.Tests.csproj`
- Framework: xUnit targeting net48
- Current test count: 24 tests (all passing)
- Tests cover: API layer characterization (factory, request body construction, error handling)

## Flow Validator Guidance: CLI

Since all validation is CLI-based (build + test + grep), flow validators should:
- Run assertions independently — no shared state concerns
- Use `rg` for code inspection assertions
- Use `dotnet build` / `dotnet test` for build/test assertions
- No isolation needed between parallel validators (all read-only operations)
