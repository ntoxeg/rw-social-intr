# User Testing

Testing surface: tools, URLs, setup steps, isolation notes, known quirks.

---

## Testing Surface
This is a RimWorld mod — no automated user testing is possible. The game cannot be launched headlessly or in CI.

## Validation Approach
- **Automated:** `dotnet build -c Release` (0 errors) + `dotnet test` (0 failures)
- **Manual:** User will test in-game after mission completion
- **No browser/TUI testing applies** — pure C# library compiled as a DLL loaded by the game
