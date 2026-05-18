# Runtime evidence runbook

Use this before making undirected Tecmo gameplay/UI changes.

## 1. Build once
```bash
dotnet build src/TecmoSB.sln
```

## 2. Pick the evidence loop

### Rules/simulation changes
Run asserted state checks first:
```bash
dotnet run --project src/TecmoSBGame -- --headless-scrimmage-pack
```
For durable before/after artifacts:
```bash
dotnet run --project src/TecmoSBGame -- --headless-determinism-check scrimmage-pack 2
```
Artifacts land under:
- `artifacts/headless-determinism/scrimmage-pack_<timestamp>/run1.json`
- `artifacts/headless-determinism/scrimmage-pack_<timestamp>/run2.json`

### Visual/runtime presentation changes
Capture the live Arch runtime frame:
```bash
dotnet run --project src/TecmoSBGame -- --runtime-capture 240
```
Artifacts land under:
- `artifacts/runtime-captures/capture_<timestamp>/frame.png`
- `artifacts/runtime-captures/capture_<timestamp>/manifest.json`

Verified current behavior: `--runtime-capture 240` writes a PNG + manifest and prints the artifact directory on success.

## 3. Compare before/after

1. Run the relevant command before editing and save the artifact path.
2. Re-run the same command after editing.
3. Compare the matching outputs:
   - runtime capture: inspect `frame.png`, then diff `manifest.json`
   - determinism check: diff `run*.json`
4. If the task changed visible behavior but you cannot point to a changed screenshot, manifest field, or scenario JSON, do not claim runtime evidence.

## 4. When the task is blocked
Treat the task as blocked if any of these are true:
- the change is mainly visual/interactive, but the current `--runtime-capture` path does not reach the affected state
- the change needs live input/window observation that this environment still cannot provide
- only code review/stdout is available and no relevant artifact changed
- the requested scenario cannot be reproduced with an existing `--headless-*`, `--headless-determinism-check`, or `--runtime-capture` path

Current honest limit: the repo has one deterministic runtime capture path, but it does not yet script arbitrary menu/navigation flows. For title/menu/input-heavy tasks, add or request a more specific capture path instead of guessing.
