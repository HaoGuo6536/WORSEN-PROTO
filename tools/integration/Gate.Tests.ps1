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
if ($failures -gt 0) { exit 1 }
