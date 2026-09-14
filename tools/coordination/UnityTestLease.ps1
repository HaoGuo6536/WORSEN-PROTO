# ============================================================================
# UnityTestLease.ps1
# ============================================================================
# PURPOSE:
#   Coordinates exclusive Unity testing and writes into an editor-open checkout.
#   A persistent record survives individual agent commands; a short-lived file
#   guard serializes every record read and mutation across processes/worktrees.
# ROLE: Repository coordination tool; does not inspect or control Unity.
# DEPENDENCIES: PowerShell 5.1+, Git, and the local filesystem only.
# USAGE: See README.md. A stale heartbeat warns; it never grants a takeover.
# ============================================================================

[CmdletBinding()]
param(
    [Parameter(Position = 0)][string]$Command = 'Status',
    [string]$Owner = '',
    [string]$Plan = '',
    [string]$Purpose = '',
    [string]$Token = '',
    [string]$ProjectPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:LeasePath = $null
$script:HeartbeatWarningSeconds = 300

function New-Response {
    param([bool]$Ok, [string]$Status, $Lease = $null, [string]$ErrorText = '')
    $publicLease = $null
    $age = $null
    $stale = $false
    $warning = $null
    if ($null -ne $Lease) {
        $heartbeat = [DateTimeOffset]::Parse($Lease.heartbeatAtUtc,
            [Globalization.CultureInfo]::InvariantCulture)
        $age = [Math]::Round(([DateTimeOffset]::UtcNow - $heartbeat).TotalSeconds, 2)
        $stale = $age -gt $script:HeartbeatWarningSeconds
        if ($stale) {
            $warning = 'Heartbeat is stale. The lease remains held; contact its owner. No automatic expiry or takeover is permitted.'
        } elseif ($age -lt 0) {
            $warning = 'Heartbeat is ahead of the local UTC clock. The lease remains held.'
        }
        $publicLease = [ordered]@{
            owner = $Lease.owner; plan = $Lease.plan; purpose = $Lease.purpose
            projectPath = $Lease.projectPath; gitCommonDirectory = $Lease.gitCommonDirectory
            acquiredAtUtc = $Lease.acquiredAtUtc; heartbeatAtUtc = $Lease.heartbeatAtUtc
            acquisitionProcessId = $Lease.acquisitionProcessId; host = $Lease.host
        }
    }
    return [ordered]@{
        schemaVersion = 1; command = $Command; ok = $Ok; status = $Status
        leasePath = $script:LeasePath; lease = $publicLease
        heartbeatAgeSeconds = $age; staleHeartbeat = $stale; warning = $warning
        error = $(if ($ErrorText) { $ErrorText } else { $null })
    }
}

function Read-Lease {
    # Called only while holding the shared guard. Only FileNotFound means free;
    # permission failures, invalid JSON, and invalid records are fail-closed.
    $recordStream = $null
    try {
        try {
            $recordStream = [IO.File]::Open($script:LeasePath, [IO.FileMode]::Open,
                [IO.FileAccess]::Read, [IO.FileShare]::Read)
        } catch [IO.FileNotFoundException] {
            return $null
        }
        $reader = New-Object IO.StreamReader($recordStream, [Text.Encoding]::UTF8)
        try { $raw = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ([string]::IsNullOrWhiteSpace($raw)) { throw 'The lease record is empty.' }
        if (-not $raw.TrimStart().StartsWith('{')) { throw 'The lease record must be an object.' }
        $jsonArguments = @{ InputObject = $raw; ErrorAction = 'Stop' }
        if ((Get-Command ConvertFrom-Json).Parameters.ContainsKey('DateKind')) {
            $jsonArguments.DateKind = 'String'
        }
        $lease = ConvertFrom-Json @jsonArguments
        if ($null -eq $lease -or $lease -is [Array]) { throw 'The lease record must be an object.' }
        $required = @('schemaVersion', 'token', 'owner', 'plan', 'purpose', 'projectPath',
            'gitCommonDirectory', 'acquiredAtUtc', 'heartbeatAtUtc', 'acquisitionProcessId', 'host')
        foreach ($field in $required) {
            if ($lease.PSObject.Properties.Name -notcontains $field) {
                throw "The lease record is missing '$field'."
            }
        }
        if (($lease.schemaVersion -isnot [int] -and $lease.schemaVersion -isnot [long]) -or
            $lease.schemaVersion -ne 1) { throw 'Unsupported lease schema version.' }
        foreach ($timestamp in @('acquiredAtUtc', 'heartbeatAtUtc')) {
            if ($lease.$timestamp -is [DateTime]) {
                $lease.$timestamp = $lease.$timestamp.ToUniversalTime().ToString('o')
            }
        }
        foreach ($field in @('token', 'owner', 'plan', 'purpose', 'projectPath',
                'gitCommonDirectory', 'acquiredAtUtc', 'heartbeatAtUtc', 'host')) {
            if ($lease.$field -isnot [string] -or [string]::IsNullOrWhiteSpace($lease.$field)) {
                throw "The lease field '$field' must be a nonempty string."
            }
        }
        $parsedToken = [Guid]::Empty
        if (-not [Guid]::TryParseExact($lease.token, 'D', [ref]$parsedToken) -or
            $parsedToken -eq [Guid]::Empty) { throw 'Invalid lease token.' }
        $acquired = [DateTimeOffset]::Parse($lease.acquiredAtUtc,
            [Globalization.CultureInfo]::InvariantCulture)
        $heartbeat = [DateTimeOffset]::Parse($lease.heartbeatAtUtc,
            [Globalization.CultureInfo]::InvariantCulture)
        if ($heartbeat -lt $acquired) { throw 'Heartbeat precedes acquisition.' }
        if (-not [IO.Path]::IsPathRooted($lease.projectPath) -or
            -not [IO.Path]::IsPathRooted($lease.gitCommonDirectory)) {
            throw 'Lease paths must be absolute.'
        }
        if ($lease.gitCommonDirectory -ne $script:GitCommonDirectory) {
            throw 'Lease record belongs to a different Git common directory.'
        }
        if ($lease.acquisitionProcessId -isnot [int] -and
            $lease.acquisitionProcessId -isnot [long]) { throw 'Invalid acquisition process ID.' }
        if ($lease.acquisitionProcessId -le 0) { throw 'Invalid acquisition process ID.' }
        return $lease
    } finally {
        if ($null -ne $recordStream) { $recordStream.Dispose() }
    }
}

function Write-Lease {
    param($Lease, [bool]$Replacing)
    # Write/flush a new sibling, then atomically publish it. A crashed heartbeat
    # leaves the old valid record; a crashed acquire never authorizes its caller.
    $temporaryPath = $script:LeasePath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes(
            ($Lease | ConvertTo-Json -Depth 5 -Compress))
        $stream = [IO.File]::Open($temporaryPath, [IO.FileMode]::CreateNew,
            [IO.FileAccess]::Write, [IO.FileShare]::None)
        try {
            $stream.Write($bytes, 0, $bytes.Length)
            $stream.Flush($true)
        } finally { $stream.Dispose() }
        if ($Replacing) {
            [IO.File]::Replace($temporaryPath, $script:LeasePath, [NullString]::Value)
        } else {
            [IO.File]::Move($temporaryPath, $script:LeasePath)
        }
    } finally {
        # File.Delete is idempotent and targets only this invocation's sibling.
        [IO.File]::Delete($temporaryPath)
    }
}

function Invoke-GuardedCommand {
    $lease = Read-Lease
    if ($Command -eq 'Status') {
        $status = if ($null -eq $lease) { 'free' } else { 'held' }
        return @{ Code = 0; Body = (New-Response $true $status $lease) }
    }
    if ($Command -eq 'Acquire') {
        if ($null -ne $lease) {
            return @{ Code = 20; Body = (New-Response $false 'busy' $lease) }
        }
        $now = [DateTimeOffset]::UtcNow.ToString('o')
        $lease = [ordered]@{
            schemaVersion = 1; token = [Guid]::NewGuid().ToString('D')
            owner = $Owner; plan = $Plan; purpose = $Purpose
            projectPath = $script:ResolvedProjectPath
            gitCommonDirectory = $script:GitCommonDirectory
            acquiredAtUtc = $now; heartbeatAtUtc = $now
            acquisitionProcessId = $PID; host = [Environment]::MachineName
        }
        Write-Lease $lease $false
        $response = New-Response $true 'acquired' ([pscustomobject]$lease)
        $response['token'] = $lease.token
        return @{ Code = 0; Body = $response }
    }
    if ($null -eq $lease -or $lease.token -cne $Token) {
        return @{ Code = 21; Body = (New-Response $false 'not_owner' $lease) }
    }
    switch ($Command) {
        'AssertOwner' { return @{ Code = 0; Body = (New-Response $true 'owned' $lease) } }
        'Heartbeat' {
            $previous = [DateTimeOffset]::Parse($lease.heartbeatAtUtc,
                [Globalization.CultureInfo]::InvariantCulture)
            $now = [DateTimeOffset]::UtcNow
            if ($now -gt $previous) { $lease.heartbeatAtUtc = $now.ToString('o') }
            Write-Lease $lease $true
            return @{ Code = 0; Body = (New-Response $true 'heartbeat' $lease) }
        }
        'Release' {
            [IO.File]::Delete($script:LeasePath)
            return @{ Code = 0; Body = (New-Response $true 'released' $lease) }
        }
    }
    throw 'Unrecognized guarded command.'
}

$exitCode = 22
$response = $null
$guard = $null
try {
    if ($Command -notin @('Status', 'Acquire', 'AssertOwner', 'Heartbeat', 'Release')) {
        $exitCode = 23
        throw 'Command must be Status, Acquire, AssertOwner, Heartbeat, or Release.'
    }
    if ($Command -eq 'Acquire' -and (
            [string]::IsNullOrWhiteSpace($Owner) -or [string]::IsNullOrWhiteSpace($Plan) -or
            [string]::IsNullOrWhiteSpace($Purpose))) {
        $exitCode = 23
        throw 'Acquire requires nonempty Owner, Plan, and Purpose.'
    }
    if ($Command -in @('AssertOwner', 'Heartbeat', 'Release') -and
            [string]::IsNullOrWhiteSpace($Token)) {
        $exitCode = 23
        throw "$Command requires Token."
    }
    if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
        $ProjectPath = Join-Path $PSScriptRoot '../..'
    }
    $projectItem = Get-Item -LiteralPath $ProjectPath -Force -ErrorAction Stop
    if (-not $projectItem.PSIsContainer) { throw 'ProjectPath must be a directory in the repository.' }
    $script:ResolvedProjectPath = [IO.Path]::GetFullPath($projectItem.FullName)
    $gitOutput = & git -C $script:ResolvedProjectPath rev-parse --path-format=absolute --git-common-dir 2>&1
    if ($LASTEXITCODE -ne 0 -or @($gitOutput).Count -ne 1) {
        throw "Cannot resolve Git common directory: $($gitOutput -join ' ')"
    }
    $commonItem = Get-Item -LiteralPath ([string]$gitOutput) -Force -ErrorAction Stop
    if (-not $commonItem.PSIsContainer) { throw 'Git common directory must be a directory.' }
    $script:GitCommonDirectory = [IO.Path]::GetFullPath($commonItem.FullName)
    $coordinationDirectory = Join-Path $script:GitCommonDirectory 'worsen-coordination'
    # Concurrent directory creation is idempotent. Every lease record operation
    # after this bootstrap is protected by the same exclusive guard handle.
    [void][IO.Directory]::CreateDirectory($coordinationDirectory)
    $script:LeasePath = Join-Path $coordinationDirectory 'unity-test-lease.json'
    $guardPath = Join-Path $coordinationDirectory 'unity-test-lease.guard'
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($null -eq $guard) {
        try {
            $guard = [IO.File]::Open($guardPath, [IO.FileMode]::OpenOrCreate,
                [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        } catch [IO.IOException] {
            if ($timer.Elapsed.TotalSeconds -ge 5) {
                $exitCode = 20
                throw 'The coordination guard is busy or unavailable; no ownership was granted.'
            }
            Start-Sleep -Milliseconds 25
        }
    }
    $result = Invoke-GuardedCommand
    $response = $result.Body
    $exitCode = $result.Code
} catch {
    $status = if ($exitCode -eq 23) { 'invalid_arguments' } elseif ($exitCode -eq 20) {
        'guard_busy'
    } else { 'error' }
    $response = New-Response $false $status $null $_.Exception.Message
} finally {
    if ($null -ne $guard) { $guard.Dispose() }
}

$response | ConvertTo-Json -Depth 6 -Compress
exit $exitCode
