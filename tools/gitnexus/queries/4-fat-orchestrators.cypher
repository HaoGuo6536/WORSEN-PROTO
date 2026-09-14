// §13c query 4 — any Orchestrator whose method bodies exceed a handful of statements.
// §6: an Orchestrator is stateless and thin. Each handler translates a payload and
// forwards it, one to three lines. If it needs to remember anything, that memory is
// a phase in the originating system's BehaviorState, not a field here.
//
// Line span is a proxy for statement count; 12 lines is the "handful" threshold and
// is deliberately generous. A hit is a prompt to read the method, not an automatic
// failure — but a genuinely fat Orchestrator method is logic that belongs in the
// system that published the event.
MATCH (m:Method)
WHERE m.filePath STARTS WITH 'Assets/Scripts/Orchestrator/'
  AND m.endLine - m.startLine > 12
RETURN m.filePath AS file, m.name AS method,
       m.startLine AS start_line, m.endLine - m.startLine AS line_span
ORDER BY line_span DESC
