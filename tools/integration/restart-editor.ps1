param(
    # Restart only when the main editor's committed (private) memory exceeds this many GB.
    # Owner rule 2026-09-30: auto-restart above 20 GB, between gates only. 0 forces a restart.
    [double]$ThresholdGB = 20,
    [string]$Plan = 'PLAN-011'
)
# Restarts the main WORSEN-PROTO editor when it has grown past the threshold. The editor leaks about
# 2.5 GB per gate; at 38 GB the Edit Mode suite took ~2.5 h instead of 8-12 min.
# Under the Unity lease:
#   1. Refuse when the editor is busy, not this project, or has an unsaved scene.
#   2. Save the remaining dirty project assets (shaders excluded) after backing them up under Logs/.
#   3. Exit directly (EditorApplication.Exit; delayCall never fires in a throttled editor).
#   4. Relaunch, wait for the bridge and an idle editor, record memory, release.
# The lease token is kept under Logs/AgentValidation/EditorRestart for the documented recovery path.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Common.ps1"
$main = $script:Main
$version = ((Get-Content -LiteralPath (Join-Path $main 'ProjectSettings\ProjectVersion.txt')) -match '^m_EditorVersion: ')[0] -replace '^m_EditorVersion: ', ''
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path -LiteralPath $unity)) { throw "Unity $version not found at $unity" }
$editor = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -match [regex]::Escape($main) -and $_.CommandLine -notmatch '-batchMode' } | Select-Object -First 1
if (-not $editor) { "No open editor for $main; nothing to restart."; return }
$oldPid = [int]$editor.ProcessId
$privateGB = (Get-Process -Id $oldPid).PrivateMemorySize64 / 1GB
if ($ThresholdGB -gt 0 -and $privateGB -le $ThresholdGB) { "Editor PID $oldPid at {0:n1} GB committed; at or below {1} GB, no restart." -f $privateGB, $ThresholdGB; return }
"Editor PID $oldPid at {0:n1} GB committed; restarting (threshold {1} GB)." -f $privateGB, $ThresholdGB
$evidence = Join-Path $main 'Logs\AgentValidation\EditorRestart'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$tokenFile = Join-Path $evidence 'lease-token.txt'
$token = Enter-UnityLease $Plan ("Editor restart (owner rule: above {0} GB; at {1:n1} GB)" -f $ThresholdGB, $privateGB)
Set-Content -LiteralPath $tokenFile -Value $token -NoNewline
$safe = $false; $exited = $false; $mutated = $false
try {
    $s = Get-EditorState
    "before: main=$(Test-EditorIsMain $s) idle=$(Test-EditorIdle $s) dirtyScene=$($s.Dirty) scene=$($s.Scene)"
    if (-not (Test-EditorIsMain $s) -or -not (Test-EditorIdle $s) -or $s.Dirty) { throw 'Editor busy, not main, or scene dirty; not restarting.' }
    # Shaders report dirty from compile state and never hold unsaved text edits; count everything else.
    $dirty = Invoke-UnityCsharp 'int n = 0; int skipped = 0; var sb = new System.Text.StringBuilder(); foreach (var o in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Object>()) { try { if (o == null || !UnityEditor.EditorUtility.IsPersistent(o) || !UnityEditor.EditorUtility.IsDirty(o)) { continue; } var p = UnityEditor.AssetDatabase.GetAssetPath(o); if (!(p.StartsWith("Assets/") || p.StartsWith("ProjectSettings/"))) { continue; } if (o is UnityEngine.Shader || o is UnityEngine.ComputeShader) { continue; } n++; sb.Append(p + ";"); } catch (System.Exception) { skipped++; } } return n + "|" + sb + "|skipped=" + skipped;' 180
    "dirty project assets (shaders excluded): $dirty"
    $paths = @(($dirty -split '\|')[1] -split ';' | Where-Object { $_ } | Select-Object -Unique)
    if ($paths.Count -gt 0) {
        # Save rather than discard: a written change is visible in git and reversible; lost in-memory state is not.
        $backup = Join-Path $evidence ('pre-save-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
        foreach ($p in $paths) { $dst = Join-Path $backup $p; New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dst) | Out-Null; Copy-Item -LiteralPath (Join-Path $main $p) -Destination $dst -Force }
        Assert-UnityLease $token
        $mutated = $true
        "save: " + (Invoke-UnityCsharp 'UnityEditor.AssetDatabase.SaveAssets(); return "saved";' 180)
        foreach ($p in $paths) { $a = Get-FileHash -LiteralPath (Join-Path $backup $p); $b = Get-FileHash -LiteralPath (Join-Path $main $p); "  saved $p : " + $(if ($a.Hash -eq $b.Hash) { 'no byte change' } else { 'CHANGED (compare with the backup)' }) }
        "backup of the pre-save files: $backup"
    }
    $before = Get-Process -Id $oldPid
    "old editor: private {0:n1} GB, working set {1:n1} GB" -f ($before.PrivateMemorySize64/1GB), ($before.WorkingSet64/1GB)
    Assert-UnityLease $token
    # delayCall never fires in a throttled, unfocused editor; exit directly (the reply may be lost as the process ends).
    $mutated = $true
    try { "exit: " + (Invoke-UnityCsharp 'UnityEditor.EditorApplication.Exit(0); return "exiting";' 60) } catch { "exit call ended without a reply (expected): $($_.Exception.Message)" }
    $deadline = (Get-Date).AddMinutes(4)
    while ((Get-Date) -lt $deadline) { if (-not (Get-Process -Id $oldPid -ErrorAction SilentlyContinue)) { $exited = $true; break }; Start-Sleep -Seconds 5; Invoke-LeaseCommand Heartbeat $token | Out-Null }
    if (-not $exited) { throw 'Editor did not exit within 4 minutes; lease retained, nothing relaunched.' }
    "old editor exited at $(Get-Date -Format HH:mm:ss); import workers: $((Get-Process Unity -ErrorAction SilentlyContinue | Measure-Object).Count) Unity process(es) left"
    Start-Process -FilePath $unity -ArgumentList @('-projectPath', "`"$main`"") | Out-Null
    "relaunched at $(Get-Date -Format HH:mm:ss)"
    $s = Wait-EditorIdle $token 30
    if (-not $s) { throw 'Relaunched editor did not become idle within 30 minutes; lease retained.' }
    "after: main=$(Test-EditorIsMain $s) idle=$(Test-EditorIdle $s) failed=$($s.Failed) scene=$($s.Scene) at $(Get-Date -Format HH:mm:ss)"
    if (-not (Test-EditorIsMain $s)) { throw 'Connected editor is not WORSEN-PROTO; lease retained.' }
    "heap: " + (Invoke-UnityCsharp 'return "monoUsed=" + (UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() >> 20) + "MB monoHeap=" + (UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() >> 20) + "MB";')
    $new = Get-Process Unity | Where-Object { $_.MainWindowTitle -like 'WORSEN-PROTO*' } | Select-Object -First 1
    if ($new) { "new editor PID {0}: private {1:n1} GB, working set {2:n1} GB" -f $new.Id, ($new.PrivateMemorySize64/1GB), ($new.WorkingSet64/1GB) }
    $os = Get-CimInstance Win32_OperatingSystem; "system commit free {0:n1} GB of {1:n1} GB" -f ($os.FreeVirtualMemory/1MB), ($os.TotalVirtualMemorySize/1MB)
    $safe = $true
} finally {
    if (-not $safe -and -not $mutated) { try { $s2 = Get-EditorState; if (Test-EditorIdle $s2) { $safe = $true; 'Nothing was changed and the editor is idle; releasing the lease.' } } catch { } }
    if ($safe) { Exit-UnityLease $token; Remove-Item -LiteralPath $tokenFile -Force -ErrorAction SilentlyContinue; 'Lease released.' } else { "LEASE RETAINED ($($token.Substring(0,8))...): verify the editor, then release per tools/coordination/README.md." }
}
