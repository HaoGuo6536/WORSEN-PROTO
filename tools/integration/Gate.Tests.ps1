# Self-test for Gate.ps1 (plain PowerShell; run: powershell -NoProfile -File tools/integration/Gate.Tests.ps1).
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Gate.ps1"
$failures = 0
function Check([string]$name, [bool]$ok) { if ($ok) { "PASS $name" } else { "FAIL $name"; $script:failures++ } }
function F([string]$n, [string[]]$c = @()) { [pscustomobject]@{ name = $n; categories = $c; message = '' } }
$today = [datetime]'2026-10-01'
$q = @(
    [pscustomobject]@{ match = 'category:RequiresFocus'; expires = '2026-12-31' },
    [pscustomobject]@{ match = 'Worsen.Tests.Flaky.*'; expires = '2026-12-31' },
    [pscustomobject]@{ match = 'Worsen.Tests.Old.*'; expires = '2026-09-01' }
)
$base = [pscustomobject]@{ label = 'b'; failed_names = @('A.x', 'A.y', 'Worsen.Tests.Old.z'); blocking_count = 3 }

$r = [pscustomobject]@{ total = 10; failures = @((F 'A.x'), (F 'Focus.t' @('RequiresFocus')), (F 'Worsen.Tests.Flaky.q')) }
$v = Get-GateVerdict $r $q $base $today
Check 'known failures below baseline pass' ($v.pass -and $v.blocking.Count -eq 1 -and $v.quarantined.Count -eq 2)

$r = [pscustomobject]@{ total = 10; failures = @((F 'A.x'), (F 'B.new')) }
$v = Get-GateVerdict $r $q $base $today
Check 'a new failure fails' (-not $v.pass -and $v.new -contains 'B.new')

$r = [pscustomobject]@{ total = 10; failures = @((F 'Worsen.Tests.Old.z')) }
$v = Get-GateVerdict $r $q $base $today
Check 'expired quarantine blocks but is known' ($v.pass -and $v.blocking -contains 'Worsen.Tests.Old.z')

$r = [pscustomobject]@{ total = 10; failures = @((F 'A.x'), (F 'A.y'), (F 'Worsen.Tests.Old.z')) }
$v = Get-GateVerdict $r $q ([pscustomobject]@{ failed_names = @('A.x', 'A.y', 'Worsen.Tests.Old.z'); blocking_count = 2 }) $today
Check 'ratchet: more blocking than baseline fails' (-not $v.pass)

$v = Get-GateVerdict ([pscustomobject]@{ total = 0; failures = @() }) $q $base $today
Check 'zero tests fail' (-not $v.pass)

$v = Get-GateVerdict ([pscustomobject]@{ total = 5; failures = @() }) $q $null $today
Check 'green with no baseline passes' ($v.pass)
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("gate-drift-" + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Force -Path (Join-Path $tmp 'Assets/Resources/X') | Out-Null
    Set-Content -LiteralPath (Join-Path $tmp 'Assets/Resources/X/a.asset') -Value 'a'
    Set-Content -LiteralPath (Join-Path $tmp 'Assets/Resources/X/b.asset') -Value 'b'
    Set-Content -LiteralPath (Join-Path $tmp 'Assets/Resources/X/c.png') -Value 'ignored'
    $s1 = Get-GeneratedSnapshot $tmp
    Check 'snapshot covers generated extensions only' ($s1.Count -eq 2 -and $s1.ContainsKey('Assets/Resources/X/a.asset'))
    Set-Content -LiteralPath (Join-Path $tmp 'Assets/Resources/X/a.asset') -Value 'a2'
    Remove-Item -LiteralPath (Join-Path $tmp 'Assets/Resources/X/b.asset')
    Set-Content -LiteralPath (Join-Path $tmp 'Assets/Resources/X/d.prefab') -Value 'd'
    $d = Compare-GeneratedSnapshot $s1 (Get-GeneratedSnapshot $tmp)
    Check 'drift reports changed, added and removed' (($d.changed -join ',') -eq 'Assets/Resources/X/a.asset' -and ($d.added -join ',') -eq 'Assets/Resources/X/d.prefab' -and ($d.removed -join ',') -eq 'Assets/Resources/X/b.asset')
    $d = Compare-GeneratedSnapshot $s1 $s1
    Check 'identical snapshots show no drift' ($d.changed.Count + $d.added.Count + $d.removed.Count -eq 0)
} finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
if ($failures -gt 0) { exit 1 }
