# GitNexus conformance queries (§13c)

Saved Cypher queries that check relationships *between* files — the things
ast-grep cannot see because they do not live inside a single syntax tree.

Run one:

```bash
gitnexus cypher --repo WORSEN-PROTO "$(cat tools/gitnexus/queries/1-cross-layer-domain-presentation.cypher)"
```

Run them all in CI, alongside `ast-grep scan`. **Every query is expected to
return zero rows.** Query 4 is the one judgement call: a hit there is a prompt to
read the method, not an automatic failure.

| File | Checks | § |
|---|---|---|
| `1-cross-layer-domain-presentation.cypher` | Domain/Session ↔ Presentation references, in both directions | §9 |
| `2-domain-system-cycles.cypher` | Direct cycles between two Domain systems | §2c |
| `3-driver-calls-game-system.cypher` | A Driver calling a Manager, Controller or Registry | §7a, §9 |
| `4-fat-orchestrators.cypher` | Orchestrator methods longer than a handful of statements | §6 |
| `5-untested-pure-layer-logic.cypher` | A Controller or Presenter no test file references | §11 |

## Graph schema these queries assume

Verified against the index on 2026-09-14 (GitNexus 1.6.10, Kuzu backend):

- **Node tables**: `File`, `Folder`, `Class`, `Struct`, `Interface`, `Enum`,
  `Method`, `Function`, `Constructor`, `Property`, `Const`, `Namespace`,
  `Variable`, plus graph-derived `Community`, `Process`, `Section`, `Route`.
- **Symbol properties**: `name`, `filePath` (repo-relative, forward slashes),
  `startLine`, `endLine`, `content`.
- **Relationships**: a single table `CodeRelation`, discriminated by `r.type`:
  `CALLS`, `DEFINES`, `HAS_METHOD`, `MEMBER_OF`, `CONTAINS`, `ACCESSES`,
  `HAS_PROPERTY`, `USES`, `METHOD_OVERRIDES`, `EXTENDS`, `IMPORTS`.
- **Dialect notes**: this is Kuzu, not Neo4j. `type(r)` does not exist — filter on
  `r.type`. Lists are **1-based**, so `string_split(p, '/')[4]` is the system
  folder under `Assets/Scripts/Domain/`. Multi-label match is `(a:Class|Method)`.
  Untyped node patterns silently yield no property values; always name labels.

Re-verify this section after a GitNexus upgrade:

```bash
gitnexus cypher --repo WORSEN-PROTO "CALL show_tables() RETURN *"
gitnexus cypher --repo WORSEN-PROTO "MATCH ()-[r:CodeRelation]->() RETURN r.type AS t, count(*) AS c ORDER BY c DESC"
```

## Workflow (§13c)

- **Before any change**, run impact analysis on the files you intend to touch:
  `gitnexus impact <symbol> --repo WORSEN-PROTO`. A blast radius that crosses a
  layer boundary needs an event, not an edit.
- **After any change**, re-index so the graph is current for the next agent:
  `gitnexus analyze .`
