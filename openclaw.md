# openclaw.md

## Project context
- Read `/home/montblanc/.openclaw/workspace/Projects/tectonic-super-bowl-clone/context.md` for project history, known issues, and thread-derived decisions.
- Read `/home/montblanc/repos/tecmo-super-bowl-monogame/OPENCLAW_TASKS.md` for the current executable task list.

## Workflow
- Prefer bounded local changes.
- Validate with:
  - `dotnet build src/TecmoSB.sln`
  - `dotnet run --project src/TecmoSBGame -- --headless-2plays 240`
- Use the workspace project hub only for summary/history, not as the executable task source.
