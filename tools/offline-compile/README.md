# Offline compile harness

Compiles the project's assemblies with Unity's bundled Roslyn (`DotNetSdkRoslyn/csc.dll`) and reference assemblies, without starting Unity. Moved here from `Logs/AgentValidation/PLAN-002/offline-compile/` so the gate is versioned; evidence still goes to `Logs/AgentValidation/PLAN-002/offline-compile/<RunName>/` (git-ignored).

| Script | Purpose |
|---|---|
| `Compile-Staged.ps1 -Plans @() -Through Tests -RunName <name> -ProjectRoot <checkout>` | Snapshot `.cs`/`.asmdef` inputs, compile Core → Tests in order with references derived from each asmdef, and record hashes and drift. Usually called through `tools/integration/compile.ps1`. |
| `Lint-Snapshot.ps1` | ast-grep over a preserved snapshot. |
| `Run-ManagedPure.ps1` + `ManagedPureRunner.cs` | Executes selected pure NUnit fixtures by reflection on .NET (not the Unity Test Framework). |

Requirements: Unity 6000.3.12f1 installed at the Hub default path, and a checkout whose `Library/ScriptAssemblies` exists (worktrees junction the main `Library`). Each run name must be new; the harness refuses to overwrite evidence.
