# ============================================================================
# Run-ManagedPure.ps1
# ============================================================================
# PURPOSE:
#   Retains the old five-fixture entry point while sharing the generic headless
#   runner. Existing SnapshotName/ProjectRoot arguments map to CompileRun/Worktree;
#   output now uses PURE_RESULT and the generic per-case evidence schema.
# ARCHITECTURAL ROLE: Offline verification adapter; no project runtime layer.
# KEY RESPONSIBILITIES:
#   - Preserve the historic fixture selection and argument names.
#   - Forward execution and its exit code to Run-PureTests.ps1.
# DEPENDENCIES: Run-PureTests.ps1 and a successful offline compile snapshot.
# USAGE NOTES: Does not launch/contact Unity; old engine exclusions are classified.
# ============================================================================
param(
    [Parameter(Mandatory = $true)][string]$SnapshotName,
    [Parameter(Mandatory = $true)][string]$RunName,
    [string]$ProjectRoot = (Get-Location).Path,
    [string]$Filter = '^Worsen[.]Tests[.](Level[.](LevelGraphUtilityTests|LevelControllerTests)|Player[.]PlayerMoverPresenterTests|Run[.]RunSessionControllerTests|SceneFlow[.]SceneFlowControllerTests)[.]'
)
& powershell -NoProfile -File (Join-Path $PSScriptRoot 'Run-PureTests.ps1') -Worktree $ProjectRoot -CompileRun $SnapshotName -RunName $RunName -Filter $Filter
exit $LASTEXITCODE
