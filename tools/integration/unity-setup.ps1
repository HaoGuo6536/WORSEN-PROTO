param(
    [Parameter(Mandatory = $true)][string]$Purpose,
    # Deterministic §10 setup methods as "Worsen.Editor.<System>.<Type>::<Method>" (public static, no arguments).
    [string[]]$Steps = @(),
    # Extra one-line C# snippets; each must end with one top-level `return "OK ..."` or `return "FAIL ..."`.
    [string[]]$Snippets = @(),
    [string]$Plan = 'PLAN-011',
    [string]$Token = '',
    [string]$OutJson = '',
    [switch]$ContinueOnError
)
# Runs setup steps in the open editor under the Unity lease and reports a structured result per
# step. A step fails on an exception, a missing type or method, or a lost result (usually a domain
# reload); a lost result is retried once after the editor settles. Exit 1 if any step failed.
# With -Token the caller already holds the lease and keeps it; otherwise this script acquires
# and releases it.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Common.ps1"
function Step-Code([string]$spec) {
    $type, $method = $spec -split '::', 2
    return ('var t = System.Type.GetType("{0}, Worsen.Editor"); var m = t == null ? null : t.GetMethod("{1}", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, System.Type.EmptyTypes, null); string r; if (t == null) {{ r = "FAIL type not found"; }} else if (m == null) {{ r = "FAIL method not found"; }} else {{ try {{ var v = m.Invoke(null, null); r = "OK" + (v == null ? "" : " " + v); }} catch (System.Exception e) {{ var x = e.InnerException ?? e; r = "FAIL " + x.GetType().Name + ": " + x.Message; }} }} return r;' -f $type, $method)
}
$owned = [string]::IsNullOrEmpty($Token)
if ($owned) { $Token = Enter-UnityLease $Plan $Purpose }
$results = @(); $safe = $false; $failed = $false
try {
    Assert-UnityLease $Token
    $s = Get-EditorState
    if (-not (Test-EditorIsMain $s) -or -not (Test-EditorIdle $s) -or $s.Failed) { throw "Editor not idle, compile failed, or wrong project: $($s | ConvertTo-Json -Compress)" }
    $work = @($Steps | ForEach-Object { [pscustomobject]@{ Name = $_; Code = (Step-Code $_) } }) +
            @($Snippets | ForEach-Object -Begin { $i = 0 } -Process { $i++; [pscustomobject]@{ Name = "snippet-$i"; Code = $_ } })
    foreach ($w in $work) {
        Assert-UnityLease $Token
        $status = 'fail'; $detail = ''
        for ($attempt = 1; $attempt -le 2; $attempt++) {
            try { $detail = Invoke-UnityCsharp $w.Code 300; $status = if ($detail -match '^OK') { 'ok' } else { 'fail' }; break }
            catch { $detail = "no result: $($_.Exception.Message)"; $status = 'unknown'; if ($attempt -eq 1) { Wait-EditorIdle $Token 10 | Out-Null } }
        }
        $results += [pscustomobject]@{ step = $w.Name; status = $status; detail = $detail }
        "{0} {1}: {2}" -f $status.ToUpper(), $w.Name, $detail
        Invoke-LeaseCommand Heartbeat $Token | Out-Null
        if ($status -ne 'ok') { $failed = $true; if (-not $ContinueOnError) { break } }
    }
    $s = Wait-EditorIdle $Token 10
    if ($s) { $safe = $true; if ($s.Failed) { $failed = $true; 'FAIL editor reports script compilation failed after setup' } }
} finally {
    if ($OutJson) { $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutJson -Encoding UTF8 }
    if ($owned) {
        if ($safe) { Exit-UnityLease $Token; 'Lease released.' } else { "LEASE RETAINED token=$Token (editor state unverified)" }
    }
}
if ($failed) { exit 1 }
