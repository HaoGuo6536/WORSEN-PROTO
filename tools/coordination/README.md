# Exclusive Unity lease

`UnityTestLease.ps1` provides one persistent, cooperative lease for this Git repository. It covers both Unity tests and **publishing, saving, generating, or importing changes in `Assets/`, `Packages/`, or `ProjectSettings/` into a checkout open in Unity**, including generated `.meta` files. Hold the lease until resulting imports and compilation have finished.

All linked worktrees resolve the same absolute Git common directory. The target `-ProjectPath` is recorded for coordination; it does not create a second lease. Different test targets therefore cannot grant concurrent editor ownership. A separately initialized Git clone has a separate common directory: this is not a machine-wide scheduler for unrelated repositories.

Only a successful `Acquire` followed by a matching `AssertOwner` authorizes the lease holder's protected actions. **Checking `Status` for `free` does not authorize a write or test**: another process can acquire immediately after that check. The script does not intercept Unity MCP, filesystem writes, IDE saves, or the Unity Editor; every participating agent must follow this protocol.

## Required workflow

1. Prepare independent code in an isolated checkout that is not open in Unity, or prepare patches/scratch files outside the open checkout's import paths. While another agent holds the lease, do not save changes into that checkout's `Assets/`, `Packages/`, or `ProjectSettings/`.
2. Acquire the lease **before the first protected write**, test run, scene/prefab mutation, package change, Play Mode transition, or editor operation that can generate imports. The same lease covers writers and testers; a writer cannot use a prior free-status check to race a test acquisition.
3. During initial protocol admission (C0), the coordinator obtains pause/freeze acknowledgments from already-active writers that have not adopted this protocol and confirms their outstanding imports have settled. Thereafter, every cooperating writer acquires the lease before publishing: the held lease establishes the freeze, so a fresh global acknowledgment is not required for every save or test. Reconcile any newly discovered nonparticipating writer before proceeding. Keep the lease through the test's completion and compilation/import settling.
4. **After acquisition**, verify the intended Unity project's identity and inspect the actual editor state. Confirm it is not compiling, updating/importing, transitioning Play Mode, running a test, or processing a conflicting setup operation. Check dirty scenes/assets and preserve user work. A lease does not prove the editor is idle; an already-running or externally launched test may not have a lease. Wait while still owning the lease and coordinate with that operator. Never launch a second Editor against a locked project or stop someone else's test.
5. Assert ownership immediately before each protected operation; heartbeat approximately once a minute during longer work and while waiting for Unity. For tests, wait for completed results, inspect failures/counts and console output, then wait for Unity imports/compilation to settle. Source writers also keep ownership through resulting import/compile activity.
6. Release with the token returned by your successful acquisition only after the operation is confirmed complete and a fresh Unity read establishes idle state. A timeout, disconnected tool, unknown test status, or incomplete import is not completion: retain the lease and report the uncertainty. A `finally` block must use that verified release condition, not release unconditionally. When work spans separate agent tool calls, retain the token in that task's context and release explicitly after the same checks.

If the editor preflight fails or state cannot be read, do not begin the dependent operation. If the lease owner disappears, a stale heartbeat **never** authorizes takeover, deletion, or a second run. Contact the recorded owner/coordinator. If the owner is unavailable, the coordinator must confirm the original agent and writers have stopped, independently inspect the actual Unity editor as idle with no old test/import/setup still running, and record the recovery incident and evidence. Only after those checks may the coordinator read the token from the local record and call the normal token-checked `Release`, then acquire a new lease before new work. There is deliberately no force-release, automatic expiry, or steal command. A malformed record requires explicit coordinated recovery; the script fails closed and cannot release a record whose ownership it cannot validate.

## Commands

Requires Git and PowerShell 5.1 or newer. The examples use `pwsh`; `powershell.exe` can run the lease tool on Windows. Run from the repository root or set the tool's absolute path. `-ProjectPath` defaults to the repository containing this script and can identify any linked worktree.

```powershell
$leaseTool = Join-Path (Get-Location).Path 'tools/coordination/UnityTestLease.ps1'
$unityProject = (Get-Location).Path

# Read-only coordination information; this grants no permission to mutate Unity inputs.
pwsh -NoProfile -File $leaseTool -Command Status -ProjectPath $unityProject

# Use your actual agent/task identity, exact plan ID, and concrete purpose.
$raw = pwsh -NoProfile -File $leaseTool -Command Acquire `
    -Owner '/root/player-agent' -Plan 'PLAN-003' `
    -Purpose 'Publish Player implementation; wait for import; run Player Edit Mode tests' `
    -ProjectPath $unityProject
$leaseExit = $LASTEXITCODE
$lease = $raw | ConvertFrom-Json
if ($leaseExit -ne 0) { throw "Unity lease unavailable: $raw" }
$leaseToken = $lease.token
$safeToRelease = $false

try {
    # First confirm C0 admission if needed, then inspect actual Unity idle state.
    # The lease establishes the freeze for cooperating writers after admission.
    pwsh -NoProfile -File $leaseTool -Command AssertOwner -Token $leaseToken -ProjectPath $unityProject
    if ($LASTEXITCODE -ne 0) { throw 'Unity lease ownership could not be verified.' }

    # Perform the specifically authorized write/setup/test, then await completion.
    # Heartbeat during longer operations, retaining the same token:
    pwsh -NoProfile -File $leaseTool -Command Heartbeat -Token $leaseToken -ProjectPath $unityProject
    if ($LASTEXITCODE -ne 0) { throw 'Unity lease heartbeat failed; stop dependent actions.' }

    # Set this ONLY from actual completion evidence AND a fresh Unity idle read.
    # A started test, tool timeout, or disconnected editor cannot set it true.
    # $safeToRelease = $true
} finally {
    if ($safeToRelease) {
        pwsh -NoProfile -File $leaseTool -Command Release -Token $leaseToken -ProjectPath $unityProject
        if ($LASTEXITCODE -ne 0) { Write-Error 'Lease release failed; coordinate before any further Unity work.' }
    } else {
        Write-Warning 'Lease retained: completion/idle state is unverified. Report status and coordinate recovery.'
    }
}
```

The commands are `Status`, `Acquire -Owner ... -Plan ... -Purpose ...`, `AssertOwner -Token ...`, `Heartbeat -Token ...`, and `Release -Token ...`. Every command accepts `-ProjectPath`. Acquisition is deliberately not reentrant: even the same owner gets busy if a lease exists; retain and use the original token.

## JSON and exit codes

Every handled command emits one JSON object on stdout. Check both its exit code and status; errors do not mean the lease is free.

| Exit | Meaning | Status values |
| --- | --- | --- |
| `0` | Operation succeeded, or status was read | `free`, `held`, `acquired`, `owned`, `heartbeat`, `released` |
| `20` | Lease held, or short-lived guard unavailable | `busy`, `guard_busy` |
| `21` | Supplied token does not own the current lease, including no lease | `not_owner` |
| `22` | Repository resolution, record read/schema, or filesystem failure; fail closed | `error` |
| `23` | Unsupported command or missing required arguments | `invalid_arguments` |

Responses contain `schemaVersion`, `command`, `ok`, `status`, `leasePath`, public `lease` metadata (or null), `heartbeatAgeSeconds`, `staleHeartbeat`, `warning`, and `error`. Only successful `Acquire` adds the top-level `token`. `Status` and `busy` do not disclose it. A heartbeat older than 300 seconds produces a warning; the lease remains held. If the UTC clock moves backward, heartbeat timestamps never regress and a future-clock warning is reported.

The persistent record is `<absolute-git-common-dir>/worsen-coordination/unity-test-lease.json` with schema version 1 and fields `token`, `owner`, `plan`, `purpose`, `projectPath`, `gitCommonDirectory`, `acquiredAtUtc`, `heartbeatAtUtc`, `acquisitionProcessId`, and `host`. The acquisition process ID is informational: commands exit while the logical owner keeps the lease. Do not infer expiry from that process exiting. The token is a cooperation mechanism, not a security boundary against someone with filesystem access.

The sibling `unity-test-lease.guard` is opened with `FileShare.None` for each short record operation. All record reads, acquire checks, heartbeat replacement, and token-checked release occur while holding that same guard. Acquire writes and flushes a unique sibling before an atomic move into place; heartbeat uses atomic file replacement. Failed reads and malformed records never become a free lease. Crashed processes release the OS guard automatically, but the persistent ownership record remains. Do not delete the guard file while agents are active.

## Standalone tests

No Unity process, scene, real lease, or package installation is involved. The harness requires PowerShell 7+, creates isolated temporary Git repositories/worktrees, and starts subprocesses without visible windows.

```powershell
pwsh -NoProfile -File tools/coordination/UnityTestLease.Tests.ps1
# Retain the isolated fixture root for investigation, if needed:
pwsh -NoProfile -File tools/coordination/UnityTestLease.Tests.ps1 -KeepArtifacts
```

It checks the complete owner lifecycle across processes, exactly one winner from eight concurrent acquisitions, invalid tokens and byte preservation, stale ownership without takeover, malformed/read-failed records, clock rollback, required arguments, and shared worktree paths. The default cleanup verifies the absolute temporary root before recursive deletion. Failed tests return a nonzero exit code with JSON results.
