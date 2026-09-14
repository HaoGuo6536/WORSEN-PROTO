// §13c query 2 — any cycle among Domain systems, using each system's folder as the
// node. §2c requires the dependency graph inside a layer to be acyclic: if two
// systems need each other, the shared piece moves down (a Core DTO) or the
// coordination moves up (a Session Manager).
//
// This finds DIRECT (two-system) cycles, which is the case that actually occurs.
// Longer cycles (A -> B -> C -> A) need `gitnexus check` or a manual read of the
// header DEPENDENCIES lists.
//
// Expected result: zero rows.
MATCH (a:Class|Struct|Interface|Enum|Method|Function|Constructor|Property|Const)
      -[r:CodeRelation]->
      (b:Class|Struct|Interface|Enum|Method|Function|Constructor|Property|Const)
WHERE r.type IN ['CALLS', 'USES', 'EXTENDS', 'ACCESSES']
  AND a.filePath STARTS WITH 'Assets/Scripts/Domain/'
  AND b.filePath STARTS WITH 'Assets/Scripts/Domain/'
WITH string_split(a.filePath, '/')[4] AS system_a,
     string_split(b.filePath, '/')[4] AS system_b
WHERE system_a <> system_b
WITH DISTINCT system_a, system_b
WITH collect([system_a, system_b]) AS edges
UNWIND edges AS forward
UNWIND edges AS backward
WITH forward[1] AS system_a, forward[2] AS system_b, backward[1] AS back_a, backward[2] AS back_b
WHERE system_a = back_b AND system_b = back_a AND system_a < system_b
RETURN DISTINCT system_a, system_b
ORDER BY system_a, system_b
