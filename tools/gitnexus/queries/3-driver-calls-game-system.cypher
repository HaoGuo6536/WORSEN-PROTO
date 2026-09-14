// §13c query 3 — any Driver-layer file that calls a Manager, Controller or Registry.
// §7a: a Driver never pulls game state. Data flows in as primitive command
// parameters, its DriverConfig, or a read-only view its Manager hands it; data
// flows out only as C# events. A Driver that calls into a game system has
// inverted the ownership direction (§9, rung 4).
//
// Expected result: zero rows. The fix is always the same: let the Manager push
// the value in, or let the Driver publish an event the Manager subscribes to.
MATCH (a:Class|Struct|Method|Function|Constructor|Property)
      -[r:CodeRelation]->
      (b:Class|Struct|Method|Function|Constructor|Property)
WHERE r.type IN ['CALLS', 'USES', 'ACCESSES']
  AND a.filePath STARTS WITH 'Assets/Scripts/'
  AND a.filePath CONTAINS '/Driver/'
  AND (
        b.name ENDS WITH 'Manager'
        OR b.name ENDS WITH 'Controller'
        OR b.name ENDS WITH 'Registry'
      )
  AND NOT b.filePath CONTAINS '/Driver/'
RETURN a.filePath AS driver_file, a.name AS driver_symbol, r.type AS edge,
       b.name AS reached_symbol, b.filePath AS reached_file
ORDER BY driver_file, driver_symbol
