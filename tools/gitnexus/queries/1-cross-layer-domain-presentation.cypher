// §13c query 1 — any call or type reference from Domain/Session into Presentation,
// or from Presentation into Domain/Session. Both directions are forbidden by §9:
// Domain and Session never see Presentation, and Presentation never sees Domain.
// The asmdefs already make this fail to compile (§13a); this query catches it in
// review before the assemblies are wired, and proves the boundary held.
//
// Expected result: zero rows. Any row is a §9 violation — publish an event and
// route it through an Orchestrator (§6) instead of referencing across.
MATCH (a:Class|Struct|Interface|Enum|Method|Function|Constructor|Property|Const)
      -[r:CodeRelation]->
      (b:Class|Struct|Interface|Enum|Method|Function|Constructor|Property|Const)
WHERE r.type IN ['CALLS', 'USES', 'EXTENDS', 'ACCESSES', 'METHOD_OVERRIDES']
  AND (
        (
          (a.filePath STARTS WITH 'Assets/Scripts/Domain/'
            OR a.filePath STARTS WITH 'Assets/Scripts/Session/')
          AND b.filePath STARTS WITH 'Assets/Scripts/Presentation/'
        )
        OR
        (
          a.filePath STARTS WITH 'Assets/Scripts/Presentation/'
          AND (b.filePath STARTS WITH 'Assets/Scripts/Domain/'
            OR b.filePath STARTS WITH 'Assets/Scripts/Session/')
        )
      )
RETURN a.filePath AS from_file, a.name AS from_symbol, r.type AS edge,
       b.filePath AS to_file, b.name AS to_symbol
ORDER BY from_file, from_symbol
