# ============================================================================
# UnityTestLease.Tests.ps1
# ============================================================================
# PURPOSE:
#   Tests the lease through independent hidden PowerShell processes and temporary
#   Git repositories. Concurrency, malformed records, and shared worktree paths
#   are exercised without connecting to Unity or touching the real lease.
# ROLE: Standalone repository-tool tests; no Pester or package installation.
# DEPENDENCIES: PowerShell 7+, Git, UnityTestLease.ps1.
# USAGE: pwsh -NoProfile -File tools/coordination/UnityTestLease.Tests.ps1
# ============================================================================

#requires -Version 7.0
[CmdletBinding()]
param([switch]$KeepArtifacts)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$leaseScript = Join-Path $PSScriptRoot 'UnityTestLease.ps1'
$shellExecutable = (Get-Process -Id $PID).Path
$testRoot = [IO.Path]::GetFullPath([IO.Path]::Combine([IO.Path]::GetTempPath(),
    ('worsen-lease-tests-' + [Guid]::NewGuid().ToString('N'))))
$repoPath = Join-Path $testRoot 'repository with spaces'
$worktreePath = Join-Path $testRoot 'linked worktree'
$testResults = New-Object 'Collections.Generic.List[object]'
$children = New-Object 'Collections.Generic.List[object]'
$leasePath = $null
$exitCode = 0

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Start-LeaseProcess {
    param([hashtable]$Options, [string]$Barrier = '')
    if (-not $Options.ContainsKey('ProjectPath')) { $Options.ProjectPath = $repoPath }
    $payload = @{ script = $leaseScript; options = $Options; barrier = $Barrier } |
        ConvertTo-Json -Depth 4 -Compress
    $encodedPayload = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($payload))
    $childCode = @'
$ErrorActionPreference = 'Stop'
$payload = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__PAYLOAD__')) | ConvertFrom-Json
$options = @{}
foreach ($property in $payload.options.PSObject.Properties) { $options[$property.Name] = $property.Value }
if ($payload.barrier) {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (-not [IO.File]::Exists($payload.barrier)) {
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Concurrency barrier timed out.' }
        [Threading.Thread]::Sleep(5)
    }
}
& $payload.script @options
exit $LASTEXITCODE
'@
    $childCode = $childCode.Replace('__PAYLOAD__', $encodedPayload)
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $shellExecutable
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
            '-EncodedCommand', [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($childCode)))) {
        $start.ArgumentList.Add($argument)
    }
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    [void]$process.Start()
    $child = [pscustomobject]@{
        Process = $process
        OutputTask = $process.StandardOutput.ReadToEndAsync()
        ErrorTask = $process.StandardError.ReadToEndAsync()
    }
    $children.Add($child)
    return $child
}

function Finish-LeaseProcess {
    param($Child)
    if (-not $Child.Process.WaitForExit(20000)) {
        $Child.Process.Kill($true)
        throw 'Lease subprocess timed out.'
    }
    $output = $Child.OutputTask.GetAwaiter().GetResult().Trim()
    $errorText = $Child.ErrorTask.GetAwaiter().GetResult().Trim()
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($output)) "No JSON output. stderr: $errorText"
    try { $body = $output | ConvertFrom-Json -ErrorAction Stop } catch {
        throw "Invalid JSON from lease subprocess: $output; stderr: $errorText"
    }
    return [pscustomobject]@{ ExitCode = $Child.Process.ExitCode; Body = $body; Raw = $output }
}

function Invoke-Lease {
    param([hashtable]$Options)
    return Finish-LeaseProcess (Start-LeaseProcess $Options)
}

function Acquire-Fixture {
    return Invoke-Lease @{ Command = 'Acquire'; Owner = 'fixture owner'; Plan = 'PLAN-TEST';
        Purpose = 'Standalone lease validation only' }
}

function Assert-FixturePath {
    param([string]$Path)
    $absolute = [IO.Path]::GetFullPath($Path)
    $prefix = $testRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    Assert-Condition ($absolute.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) "Test mutation escaped its verified root: $absolute"
    return $absolute
}

function Remove-FixtureRecord {
    # Deliberate fixture corruption/recovery; no production record is touched.
    try { [IO.File]::Delete((Assert-FixturePath $leasePath)) } catch [IO.DirectoryNotFoundException] { }
}

function Run-TestCase {
    param([string]$Name, [scriptblock]$Body)
    try {
        & $Body
        $testResults.Add([pscustomobject]@{ name = $Name; passed = $true; error = $null })
    } catch {
        $testResults.Add([pscustomobject]@{ name = $Name; passed = $false; error = $_.Exception.Message })
        if ($null -ne $leasePath) { Remove-FixtureRecord }
    }
}

try {
    [void][IO.Directory]::CreateDirectory($repoPath)
    $gitOutput = & git init --quiet $repoPath 2>&1
    Assert-Condition ($LASTEXITCODE -eq 0) "git init failed: $gitOutput"
    $gitOutput = & git -C $repoPath -c user.name=LeaseTests -c user.email=lease-tests@example.invalid commit --quiet --allow-empty -m 'Isolated lease test fixture' 2>&1
    Assert-Condition ($LASTEXITCODE -eq 0) "git commit fixture failed: $gitOutput"
    $gitOutput = & git -C $repoPath worktree add --quiet --detach $worktreePath HEAD 2>&1
    Assert-Condition ($LASTEXITCODE -eq 0) "git worktree fixture failed: $gitOutput"
    $leasePath = Join-Path $repoPath '.git/worsen-coordination/unity-test-lease.json'
    [void](Assert-FixturePath $leasePath)

    Run-TestCase 'Status is neutral and the full owner lifecycle survives processes' {
        $free = Invoke-Lease @{ Command = 'Status' }
        Assert-Condition ($free.ExitCode -eq 0 -and $free.Body.status -eq 'free') 'Expected free status.'
        $acquired = Acquire-Fixture
        Assert-Condition ($acquired.ExitCode -eq 0 -and $acquired.Body.status -eq 'acquired') 'Acquire failed.'
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($acquired.Body.token)) 'Acquire omitted owner token.'
        $held = Invoke-Lease @{ Command = 'Status' }
        Assert-Condition ($held.Body.status -eq 'held' -and $held.Body.lease.owner -eq 'fixture owner') 'Lease did not survive process exit.'
        Assert-Condition ($held.Body.PSObject.Properties.Name -notcontains 'token') 'Status disclosed the token.'
        $owned = Invoke-Lease @{ Command = 'AssertOwner'; Token = $acquired.Body.token }
        Assert-Condition ($owned.ExitCode -eq 0 -and $owned.Body.status -eq 'owned') 'AssertOwner failed.'
        $heartbeat = Invoke-Lease @{ Command = 'Heartbeat'; Token = $acquired.Body.token }
        Assert-Condition ($heartbeat.ExitCode -eq 0 -and $heartbeat.Body.status -eq 'heartbeat') 'Heartbeat failed.'
        Assert-Condition ([DateTimeOffset]$heartbeat.Body.lease.heartbeatAtUtc -ge
            [DateTimeOffset]$acquired.Body.lease.heartbeatAtUtc) 'Heartbeat went backward.'
        $released = Invoke-Lease @{ Command = 'Release'; Token = $acquired.Body.token }
        Assert-Condition ($released.ExitCode -eq 0 -and $released.Body.status -eq 'released') 'Release failed.'
        Assert-Condition ((Invoke-Lease @{ Command = 'Status' }).Body.status -eq 'free') 'Release left a held lease.'
    }

    Run-TestCase 'Concurrent acquire has exactly one winner and cannot clobber its record' {
        $barrier = Assert-FixturePath (Join-Path $testRoot 'acquire.start')
        $racers = for ($i = 0; $i -lt 8; $i++) {
            Start-LeaseProcess @{ Command = 'Acquire'; Owner = "racer-$i"; Plan = 'PLAN-RACE';
                Purpose = 'Concurrent acquisition test' } $barrier
        }
        [IO.File]::WriteAllText($barrier, 'start')
        $raceResults = @($racers | ForEach-Object { Finish-LeaseProcess $_ })
        $winners = @($raceResults | Where-Object ExitCode -eq 0)
        $losers = @($raceResults | Where-Object ExitCode -eq 20)
        Assert-Condition ($winners.Count -eq 1) "Expected one winner; got $($winners.Count)."
        Assert-Condition ($losers.Count -eq 7) "Expected seven busy losers; got $($losers.Count)."
        $held = Invoke-Lease @{ Command = 'Status' }
        Assert-Condition ($held.Body.lease.owner -eq $winners[0].Body.lease.owner) 'Losing acquire replaced owner.'
        Assert-Condition ((Invoke-Lease @{Command='Release'; Token=$winners[0].Body.token}).ExitCode -eq 0) 'Winner release failed.'
    }

    Run-TestCase 'Invalid tokens cannot assert, heartbeat, release, or alter bytes' {
        $acquired = Acquire-Fixture
        Assert-Condition ($acquired.ExitCode -eq 0) 'Fixture acquire failed.'
        $before = [IO.File]::ReadAllText($leasePath)
        foreach ($operation in @('AssertOwner', 'Heartbeat', 'Release')) {
            $result = Invoke-Lease @{ Command = $operation; Token = [Guid]::NewGuid().ToString('D') }
            Assert-Condition ($result.ExitCode -eq 21 -and $result.Body.status -eq 'not_owner') "$operation accepted another token."
            Assert-Condition ([IO.File]::ReadAllText($leasePath) -ceq $before) "$operation changed the record."
        }
        Assert-Condition ((Invoke-Lease @{Command='Release'; Token=$acquired.Body.token}).ExitCode -eq 0) 'Owner could not release.'
        Assert-Condition ((Invoke-Lease @{Command='Heartbeat'; Token=$acquired.Body.token}).ExitCode -eq 21) 'Released token retained ownership.'
    }

    Run-TestCase 'A stale heartbeat stays held and cannot be acquired again' {
        $acquired = Acquire-Fixture
        Assert-Condition ($acquired.ExitCode -eq 0) 'Fixture acquire failed.'
        $record = Get-Content -LiteralPath $leasePath -Raw | ConvertFrom-Json
        $record.acquiredAtUtc = [DateTimeOffset]::UtcNow.AddDays(-2).ToString('o')
        $record.heartbeatAtUtc = [DateTimeOffset]::UtcNow.AddDays(-1).ToString('o')
        [IO.File]::WriteAllText((Assert-FixturePath $leasePath), ($record | ConvertTo-Json))
        $stale = Invoke-Lease @{Command='Status'}
        Assert-Condition ($stale.ExitCode -eq 0 -and $stale.Body.staleHeartbeat -eq $true) 'Stale heartbeat was not warned.'
        $busy = Acquire-Fixture
        Assert-Condition ($busy.ExitCode -eq 20 -and $busy.Body.staleHeartbeat -eq $true) 'Stale lease was stolen.'
        Assert-Condition ((Invoke-Lease @{Command='AssertOwner'; Token=$acquired.Body.token}).ExitCode -eq 0) 'Original owner expired.'
        Assert-Condition ((Invoke-Lease @{Command='Heartbeat'; Token=$acquired.Body.token}).ExitCode -eq 0) 'Original owner cannot renew stale heartbeat.'
        Assert-Condition ((Invoke-Lease @{Command='Release'; Token=$acquired.Body.token}).ExitCode -eq 0) 'Stale lease release failed.'
    }

    Run-TestCase 'Malformed and incomplete records fail closed for every command' {
        foreach ($brokenRecord in @('', '{broken json', '{"schemaVersion":1}', 'null', '[]')) {
            [IO.File]::WriteAllText((Assert-FixturePath $leasePath), $brokenRecord)
            foreach ($operation in @('Status', 'Acquire', 'AssertOwner', 'Heartbeat', 'Release')) {
                $result = Invoke-Lease @{Command=$operation; Token=[Guid]::NewGuid().ToString('D');
                    Owner='blocked'; Plan='PLAN-BLOCKED'; Purpose='Must fail closed'}
                Assert-Condition ($result.ExitCode -eq 22 -and $result.Body.ok -eq $false) "$operation accepted malformed record '$brokenRecord'."
                Assert-Condition ([IO.File]::ReadAllText($leasePath) -ceq $brokenRecord) 'Malformed record was replaced.'
            }
            Remove-FixtureRecord
        }
    }

    Run-TestCase 'A record read failure cannot be mistaken for a free lease' {
        [void][IO.Directory]::CreateDirectory((Assert-FixturePath $leasePath))
        try {
            $result = Acquire-Fixture
            Assert-Condition ($result.ExitCode -eq 22 -and $result.Body.ok -eq $false) 'Unreadable path was treated as free.'
        } finally { [IO.Directory]::Delete((Assert-FixturePath $leasePath)) }
    }

    Run-TestCase 'Clock rollback cannot poison the owner record or prevent release' {
        $acquired = Acquire-Fixture
        Assert-Condition ($acquired.ExitCode -eq 0) 'Fixture acquire failed.'
        $record = Get-Content -LiteralPath $leasePath -Raw | ConvertFrom-Json
        $record.acquiredAtUtc = [DateTimeOffset]::UtcNow.AddDays(1).ToString('o')
        $record.heartbeatAtUtc = [DateTimeOffset]::UtcNow.AddDays(2).ToString('o')
        [IO.File]::WriteAllText((Assert-FixturePath $leasePath), ($record | ConvertTo-Json))
        $heartbeat = Invoke-Lease @{Command='Heartbeat'; Token=$acquired.Body.token}
        Assert-Condition ($heartbeat.ExitCode -eq 0 -and $heartbeat.Body.warning) 'Clock rollback heartbeat failed or omitted warning.'
        Assert-Condition ([DateTimeOffset]$heartbeat.Body.lease.heartbeatAtUtc -eq
            [DateTimeOffset]$record.heartbeatAtUtc) 'Heartbeat moved backward.'
        Assert-Condition ((Invoke-Lease @{Command='AssertOwner'; Token=$acquired.Body.token}).ExitCode -eq 0) 'Clock rollback poisoned the record.'
        Assert-Condition ((Invoke-Lease @{Command='Release'; Token=$acquired.Body.token}).ExitCode -eq 0) 'Clock rollback prevented release.'
    }

    Run-TestCase 'Root arrays and noninteger schema versions cannot masquerade as valid records' {
        $acquired = Acquire-Fixture
        Assert-Condition ($acquired.ExitCode -eq 0) 'Fixture acquire failed.'
        $validRecord = [IO.File]::ReadAllText($leasePath)
        Assert-Condition ((Invoke-Lease @{Command='Release'; Token=$acquired.Body.token}).ExitCode -eq 0) 'Fixture release failed.'
        $invalidRecords = New-Object 'Collections.Generic.List[string]'
        $invalidRecords.Add('[' + $validRecord + ']')
        foreach ($invalidVersionJson in @('[]', '[1]', 'true', '"1"', '1.5')) {
            $invalidRecords.Add([Text.RegularExpressions.Regex]::Replace($validRecord,
                '"schemaVersion"\s*:\s*1', ('"schemaVersion":' + $invalidVersionJson)))
        }
        foreach ($invalidRecord in $invalidRecords) {
            [IO.File]::WriteAllText((Assert-FixturePath $leasePath), $invalidRecord)
            foreach ($operation in @('Status', 'Acquire', 'Release')) {
                $result = Invoke-Lease @{Command=$operation; Token=$acquired.Body.token;
                    Owner='blocked'; Plan='PLAN-BLOCKED'; Purpose='Must reject malformed shape'}
                Assert-Condition ($result.ExitCode -eq 22) "Malformed shape was accepted by $operation."
                Assert-Condition ([IO.File]::ReadAllText($leasePath) -ceq $invalidRecord) 'Malformed shape was rewritten.'
            }
            Remove-FixtureRecord
        }
    }

    Run-TestCase 'An unavailable exclusive guard fails busy without granting a lease' {
        $guardPath = [IO.Path]::ChangeExtension($leasePath, 'guard')
        $guard = [IO.File]::Open((Assert-FixturePath $guardPath), [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try {
            $result = Acquire-Fixture
            Assert-Condition ($result.ExitCode -eq 20 -and $result.Body.status -eq 'guard_busy') 'Guard was bypassed.'
            Assert-Condition (-not [IO.File]::Exists($leasePath)) 'Guard contention created a record.'
        } finally { $guard.Dispose() }
        Assert-Condition ((Invoke-Lease @{Command='Status'}).Body.status -eq 'free') 'Released guard remained unavailable.'
    }

    Run-TestCase 'Linked worktrees resolve the same absolute lease regardless of target path' {
        $acquired = Acquire-Fixture
        Assert-Condition ($acquired.ExitCode -eq 0) 'Fixture acquire failed.'
        $linked = Invoke-Lease @{Command='Status'; ProjectPath=$worktreePath}
        Assert-Condition ($linked.Body.leasePath -eq $acquired.Body.leasePath) 'Worktrees use different lease paths.'
        Assert-Condition ([IO.Path]::IsPathRooted($linked.Body.leasePath)) 'Lease path is relative.'
        $busy = Invoke-Lease @{Command='Acquire'; Owner='other worktree'; Plan='PLAN-OTHER';
            Purpose='Must share editor exclusion'; ProjectPath=$worktreePath}
        Assert-Condition ($busy.ExitCode -eq 20) 'Another worktree acquired concurrently.'
        Assert-Condition ((Invoke-Lease @{Command='AssertOwner'; Token=$acquired.Body.token;
            ProjectPath=$worktreePath}).ExitCode -eq 0) 'Token did not cross worktree paths.'
        Assert-Condition ((Invoke-Lease @{Command='Release'; Token=$acquired.Body.token;
            ProjectPath=$worktreePath}).ExitCode -eq 0) 'Cross-worktree owner release failed.'
    }

    Run-TestCase 'Missing arguments and nonrepository paths fail with JSON and nonzero exit' {
        $missing = Invoke-Lease @{Command='Acquire'}
        Assert-Condition ($missing.ExitCode -eq 23 -and $missing.Body.status -eq 'invalid_arguments') 'Missing owner metadata was accepted.'
        $missingToken = Invoke-Lease @{Command='Release'}
        Assert-Condition ($missingToken.ExitCode -eq 23) 'Missing token was accepted.'
        $nonrepo = Invoke-Lease @{Command='Status'; ProjectPath=$testRoot}
        Assert-Condition ($nonrepo.ExitCode -eq 22 -and $nonrepo.Body.ok -eq $false) 'Nonrepository path was accepted.'
    }

    $failures = @($testResults | Where-Object passed -eq $false)
    if ($failures.Count -gt 0) { $exitCode = 1 }
} catch {
    $exitCode = 1
    $testResults.Add([pscustomobject]@{name='Test harness'; passed=$false; error=$_.Exception.Message})
} finally {
    foreach ($child in $children) {
        if (-not $child.Process.HasExited) {
            $child.Process.Kill($true)
            $child.Process.WaitForExit()
        }
        $child.Process.Dispose()
    }
    if (-not $KeepArtifacts -and [IO.Directory]::Exists($testRoot)) {
        $resolvedTestRoot = [IO.Path]::GetFullPath((Get-Item -LiteralPath $testRoot).FullName)
        $resolvedTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd(
            [IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if ($resolvedTestRoot -ne $testRoot -or
            -not $resolvedTestRoot.StartsWith($resolvedTempRoot, [StringComparison]::OrdinalIgnoreCase) -or
            -not ([IO.Path]::GetFileName($resolvedTestRoot)).StartsWith('worsen-lease-tests-')) {
            throw "Refusing to delete unverified test root: $resolvedTestRoot"
        }
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}

[ordered]@{
    passed = @($testResults | Where-Object passed -eq $true).Count
    failed = @($testResults | Where-Object passed -eq $false).Count
    artifactRoot = $(if ($KeepArtifacts) { $testRoot } else { $null })
    results = $testResults.ToArray()
} | ConvertTo-Json -Depth 5
exit $exitCode
