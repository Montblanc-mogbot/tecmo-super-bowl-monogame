# Runtime observation loop

Smallest viable runtime-feedback loop from this environment for autonomous Tecmo work.

## Verified entry points

### 1. Build
```bash
dotnet build src/TecmoSB.sln
```
Expected result: successful build of `src/TecmoSBGame` and content pipeline execution.

### 2. Deterministic football-state validation
```bash
dotnet run --project src/TecmoSBGame -- --headless-scrimmage-pack
```
Expected result: PASS/FAIL console output for asserted scrimmage scenarios.
Artifacts: none written by this command today; feedback is stdout only.

### 3. Durable deterministic artifacts
```bash
dotnet run --project src/TecmoSBGame -- --headless-determinism-check scrimmage-pack 2
```
Expected result: PASS/FAIL plus an artifact directory path.
Artifacts: JSON files under `artifacts/headless-determinism/<scenario>_<timestamp>/run*.json`.
Current verified example path pattern:
- `artifacts/headless-determinism/scrimmage-pack_YYYYMMDD_HHMMSSfff/run1.json`
- `artifacts/headless-determinism/scrimmage-pack_YYYYMMDD_HHMMSSfff/run2.json`

## Current observable surface

- `src/TecmoSBGame/Program.cs` is the active runtime entry point.
- Headless observation is real and usable now through named `--headless-*` commands and `--headless-determinism-check`.
- The interactive MonoGame host is `src/TecmoSBGame/MainGameArch.cs` and launches with:
```bash
dotnet run --project src/TecmoSBGame
```
- In this environment, the interactive path is **not yet agent-observable**: no built-in screenshot/frame capture path was found, and the game window cannot be reliably seen or driven from here.

## Known environment limits

- No verified screenshot or rendered-frame artifact path exists in the active `MainGameArch` runtime.
- Manual interactive validation is limited by lack of visible window access from this environment.
- Existing durable artifacts are simulation-state JSON/replay-style outputs, not rendered images.
- There is older archive replay code in excluded `ArchiveMge` files, but it is not part of the active compiled runtime.

## Best current loop for autonomous work

1. `dotnet build src/TecmoSB.sln`
2. Run the closest relevant headless scenario (`--headless-scrimmage-pack` or another named `--headless-*` command)
3. For repeatability/diffable evidence, run `--headless-determinism-check <scenario> 2`
4. Inspect JSON artifacts under `artifacts/headless-determinism/`

This is enough for simulation/rules work, but **not enough for visual/UI/runtime presentation work**.

## Next implementation step

Add the first active-runtime visual artifact path to `MainGameArch`, preferably a deterministic command-line capture mode that:
- boots directly into a known scenario/state,
- advances a fixed number of ticks,
- saves a screenshot or frame set under `artifacts/runtime-captures/`, and
- prints the exact output path on success.

That is the smallest step that would make future UI/gameplay tasks observable instead of code-only.
