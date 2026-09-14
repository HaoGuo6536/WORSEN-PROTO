// §13c query 5 — every *Controller and *Presenter with no test file referencing it.
// §11: purity in the logic and presentation stacks exists so this code can be tested
// without a scene. A Controller or Presenter that nothing under Assets/Editor/Tests/
// ever touches has thrown that payoff away.
//
// This is the graph-level counterpart to the ArchitectureConformanceTests check
// (§13d), which matches on file name alone. This one proves a test actually
// REFERENCES the type, so a stub test file cannot satisfy it.
//
// Expected result: zero rows.
MATCH (c:Class)
WHERE c.filePath STARTS WITH 'Assets/Scripts/'
  AND (c.name ENDS WITH 'Controller' OR c.name ENDS WITH 'Presenter')
  AND NOT EXISTS {
        MATCH (t:Class|Method|Function|Constructor)-[:CodeRelation]->(c)
        WHERE t.filePath STARTS WITH 'Assets/Editor/Tests/'
      }
RETURN c.filePath AS file, c.name AS untested_type, c.startLine AS start_line
ORDER BY file
