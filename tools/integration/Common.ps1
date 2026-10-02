# Shared helpers for the integration gate (dot-source: . "$PSScriptRoot\Common.ps1").
# Unity is reached only through Synaptic's HTTP bridge; every mutation happens under the
# exclusive Unity lease (tools/coordination/UnityTestLease.ps1).
$script:Main = if ($env:WORSEN_MAIN) { $env:WORSEN_MAIN } else { (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
$script:Lease = Join-Path $script:Main 'tools\coordination\UnityTestLease.ps1'
$script:Bridge = if ($env:WORSEN_SYNAPTIC) { $env:WORSEN_SYNAPTIC } else { 'http://localhost:8086' }
$script:LeaseOwner = if ($env:WORSEN_LEASE_OWNER) { $env:WORSEN_LEASE_OWNER } else { 'claude-coordinator/spec-004' }
$script:Attribution = 'Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>'

# Native commands run with 'Continue' so stderr notices (Git LFS, CRLF) never become
# terminating errors; exit codes are checked explicitly instead.
function Invoke-Native([string]$exe) {
    $a = $args; $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { $o = & $exe @a 2>&1 | ForEach-Object { "$_" }; $script:NativeExit = $LASTEXITCODE } finally { $ErrorActionPreference = $prev }
    return $o
}
function Remove-StaleIndexLock([string]$dir) {
    $lock = (Invoke-Native git.exe -C $dir rev-parse --git-path index.lock | Select-Object -Last 1)
    if (-not [System.IO.Path]::IsPathRooted($lock)) { $lock = Join-Path $dir $lock }
    $item = Get-Item -LiteralPath $lock -ErrorAction SilentlyContinue
    if (-not $item -or $item.Length -ne 0) { return }
    $age = ((Get-Date) - $item.LastWriteTime).TotalSeconds
    if ($age -lt 120) { Start-Sleep -Seconds ([int][Math]::Ceiling(120 - $age)) }
    $item = Get-Item -LiteralPath $lock -ErrorAction SilentlyContinue
    if (-not $item -or $item.Length -ne 0) { return }
    for ($check = 0; $check -lt 3; $check++) {
        if (Get-Process -Name git -ErrorAction SilentlyContinue) { return }
        Start-Sleep -Seconds 2
    }
    Remove-Item -LiteralPath $lock -Force
    Write-Warning "Removed stale empty index.lock ($($item.LastWriteTime.ToString('HH:mm:ss'))) with no git process running: $lock"
}

function Invoke-Git([string]$dir) {
    # Another client (an IDE's background `git status`) can hold index.lock briefly; wait it out.
    # Batches 35-36: a git process that dies during Unity setup leaves an EMPTY index.lock behind.
    # After the retries, remove the lock only when it is empty, older than two minutes and no git
    # process is running for three consecutive checks; anything else is still reported.
    $a = $args
    for ($attempt = 1; $attempt -le 8; $attempt++) {
        $o = Invoke-Native git.exe -C $dir @a
        if ($script:NativeExit -eq 0 -or ($o -join ' ') -notmatch 'index\.lock') { break }
        if ($attempt -eq 7) { Remove-StaleIndexLock $dir }
        Start-Sleep -Seconds 5
    }
    if ($script:NativeExit -ne 0) { throw "git $($a -join ' ') failed ($($script:NativeExit)): $($o -join ' | ')" }
    $o
}

# One-line C# through Synaptic run_csharp. Requires resultSet:true; a snippet must end with a
# single top-level `return <string>;` (a return inside try/catch is silently lost).
function Invoke-UnityCsharp([string]$code, [int]$timeout = 120) {
    $one = ($code -replace "[`r`n]+", ' ')
    $body = @{ tool = 'unity_run_csharp'; params = @{ code = $one } } | ConvertTo-Json -Depth 5
    $r = Invoke-RestMethod -Uri "$script:Bridge/execute" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec $timeout
    $inner = $r.result | ConvertFrom-Json
    if (-not $inner.resultSet) { throw "C# returned no result: $($r.result)" }
    return [string]$inner.result
}
function Invoke-UnityTool([string]$name, [hashtable]$params, [int]$timeout = 120) {
    $body = @{ tool = $name; params = $params } | ConvertTo-Json -Depth 6
    return Invoke-RestMethod -Uri "$script:Bridge/execute" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec $timeout
}

$script:StateCode = 'return UnityEngine.Application.dataPath + "|" + UnityEditor.EditorApplication.isCompiling + "|" + UnityEditor.EditorApplication.isUpdating + "|" + UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode + "|" + UnityEditor.EditorUtility.scriptCompilationFailed + "|" + UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path + "|" + UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty + "|" + UnityEngine.Time.timeScale;'
function Get-EditorState {
    $p = (Invoke-UnityCsharp $script:StateCode).Split('|')
    [pscustomobject]@{ DataPath = $p[0]; Compiling = $p[1] -eq 'True'; Updating = $p[2] -eq 'True'; Playing = $p[3] -eq 'True'
        Failed = $p[4] -eq 'True'; Scene = $p[5]; Dirty = $p[6] -eq 'True'; TimeScale = [double]$p[7] }
}
function Test-EditorIsMain($s) { $s.DataPath -eq ($script:Main.Replace('\', '/') + '/Assets') }
function Test-EditorIdle($s) { -not $s.Compiling -and -not $s.Updating -and -not $s.Playing }

# Waits until the editor reports idle three polls in a row; heartbeats the lease meanwhile.
function Wait-EditorIdle([string]$token, [int]$minutes = 20) {
    $deadline = (Get-Date).AddMinutes($minutes); $idle = 0; $beat = Get-Date; $s = $null
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 5
        if (((Get-Date) - $beat).TotalSeconds -gt 50) { Invoke-LeaseCommand Heartbeat $token | Out-Null; $beat = Get-Date }
        try { $s = Get-EditorState } catch { $idle = 0; continue }
        if (Test-EditorIdle $s) { $idle++ } else { $idle = 0 }
        if ($idle -ge 3) { return $s }
    }
    return $null
}

function Invoke-LeaseCommand([string]$command, [string]$token = '', [string]$plan = 'PLAN-011', [string]$purpose = '') {
    $a = @('-NoProfile', '-File', $script:Lease, '-Command', $command, '-ProjectPath', $script:Main)
    if ($token) { $a += @('-Token', $token) }
    if ($command -eq 'Acquire') { $a += @('-Owner', $script:LeaseOwner, '-Plan', $plan, '-Purpose', $purpose) }
    $o = & powershell @a
    return [pscustomobject]@{ Exit = $LASTEXITCODE; Output = ($o -join "`n") }
}
function Enter-UnityLease([string]$plan, [string]$purpose) {
    $r = Invoke-LeaseCommand Acquire '' $plan $purpose
    if ($r.Exit -ne 0) { throw "Unity lease unavailable: $($r.Output)" }
    return ($r.Output | ConvertFrom-Json).token
}
function Assert-UnityLease([string]$token) {
    $r = Invoke-LeaseCommand AssertOwner $token
    if ($r.Exit -ne 0) { throw "Lease ownership not verified: $($r.Output)" }
}
function Exit-UnityLease([string]$token) { Invoke-LeaseCommand Release $token | Out-Null }

# Structured console capture (unity_console's read operation is broken in this Synaptic build).
function Get-ConsoleErrors([int]$limit = 100) {
    $r = Invoke-UnityTool 'unity_analyze_console_logs' @{ logType = 'error'; limit = $limit; includeStackTrace = $false }
    return ($r.result | ConvertFrom-Json)
}
