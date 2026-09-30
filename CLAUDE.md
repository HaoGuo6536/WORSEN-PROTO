@AGENTS.md

# Claude Code notes

- The shared guide above is the single source; keep project rules in `AGENTS.md`, not here.
- Project skills: `worsen-unity`, `worsen-delegation`, `worsen-art`, `worsen-audio` and `gitnexus-*`. Load one with the Skill tool when a task needs it.
- As the coordinator, launch Hermes workers only as session-bound background commands through `tools/delegation/launch.sh`, one per worker. Heartbeats and waiting belong in scripts, not in model turns.
