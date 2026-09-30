# ============================================================================
# Run-PureTests.ps1
# ============================================================================
# PURPOSE:
#   Publishes a standalone NUnit harness beside immutable offline compile inputs.
#   Runs managed tests without launching or contacting Unity, retaining evidence
#   separately from the compile snapshot and refusing existing run directories.
# ARCHITECTURAL ROLE: Offline verification tool; no project runtime layer.
# KEY RESPONSIBILITIES:
#   - Validate compile evidence and copy/hash its managed dependencies.
#   - Compile and launch the monitored NUnit harness on the bundled runtime.
#   - Preserve summaries, logs and a separately compiled harness self-test.
# DEPENDENCIES: Compile-Staged evidence, installed Roslyn/.NET, bundled NUnit.
# USAGE NOTES: Library is read-only. Exit 0 = no pure failures, 1 = test failures,
#   2 = harness/input failure or an empty selection; environment is not a pass.
# ============================================================================
param(
    [Parameter(Mandatory = $true)][string]$Worktree,
    [Parameter(Mandatory = $true)][string]$CompileRun,
    [Parameter(Mandatory = $true)][string]$RunName,
    [string]$Filter = '',
    [ValidateRange(0.1, 600)][double]$TimeoutSeconds = 10,
    [switch]$SelfTest
)
$ErrorActionPreference = 'Stop'
$wall = [Diagnostics.Stopwatch]::StartNew()
$runRoot = $null
$ownsRun = $false
try {
    foreach ($name in @($CompileRun, $RunName)) {
        if ($name -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Names must be simple directory names.' }
    }
    $Worktree = (Resolve-Path -LiteralPath $Worktree).Path
    $evidenceRoot = Join-Path $Worktree 'Logs/AgentValidation/PLAN-002/offline-compile'
    $compileRoot = Join-Path $evidenceRoot $CompileRun
    $runRoot = Join-Path $evidenceRoot $RunName
    if (Test-Path -LiteralPath $runRoot) { $runRoot = $null; throw 'Refusing to overwrite pure-test evidence.' }
    # CreateDirectory alone is not exclusive; New-Item without Force rejects a race.
    New-Item -ItemType Directory -Path $runRoot | Out-Null
    $ownsRun = $true
    $compile = Get-Content -LiteralPath (Join-Path $compileRoot 'summary.json') -Raw | ConvertFrom-Json
    if ($compile.Through -ne 'Tests' -or @($compile.Results).Count -ne 7 -or
        @($compile.Results | Where-Object { $_.Status -ne 'Compiled' -or $_.Errors -ne 0 -or $_.ExitCode -ne 0 }).Count -ne 0) {
        throw 'CompileRun must be a successful full seven-assembly compile.'
    }
    if (@($compile.SourceDriftAfterCapture).Count -ne 0) { throw 'Compile snapshot recorded source drift.' }
    $refs = Get-Content -LiteralPath (Join-Path $compileRoot 'reference-manifest.json') -Raw | ConvertFrom-Json
    if (@($refs | Where-Object { -not $_.StableAtEnd }).Count -ne 0) { throw 'Compile references drifted.' }
    $versionLine = Get-Content -LiteralPath (Join-Path $Worktree 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' }
    $version = $versionLine.Substring('m_EditorVersion: '.Length).Trim()
    if ($version -notmatch '^[0-9]+[.][0-9]+[.][0-9]+[a-z][0-9]+$') { throw 'Invalid installed editor version.' }
    $unityData = "C:/Program Files/Unity/Hub/Editor/$version/Editor/Data"
    $dotnet = Join-Path $unityData 'NetCoreRuntime/dotnet.exe'
    $compiler = Join-Path $unityData 'DotNetSdkRoslyn/csc.dll'
    $runtime = Get-ChildItem -LiteralPath (Join-Path $unityData 'NetCoreRuntime/shared/Microsoft.NETCore.App') -Directory |
        Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    foreach ($required in @($dotnet, $compiler, (Join-Path $compileRoot 'bin/Worsen.Tests.dll'))) {
        if (-not (Test-Path -LiteralPath $required)) { throw "Missing input: $required" }
    }
    $manifest = @()
    $destinations = @{}
    $dependencies = @(Get-ChildItem -LiteralPath (Join-Path $compileRoot 'bin') -Filter '*.dll' | ForEach-Object {
        [pscustomobject]@{ Path = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }) + @($refs | Where-Object { $_.Path -notmatch '[/\\]NetStandard[/\\]' })
    foreach ($dependency in $dependencies) {
        $source = $dependency.Path
        $leaf = Split-Path -Leaf $source
        if ($destinations.ContainsKey($leaf)) {
            # Unity ships two same-identity forwarding facades in its reference set.
            # Use the canonical Managed facade, not the UnityEngine subfolder variant.
            if ($leaf -in @('UnityEngine.dll', 'UnityEditor.dll') -and
                $source -match '[/\\]Managed[/\\]UnityEngine[/\\]Unity(?:Engine|Editor)[.]dll$') {
                if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $dependency.SHA256) { throw "Facade reference drift: $source" }
                $manifest += [pscustomobject]@{ SourcePath = $source; RuntimePath = $null; SHA256 = $dependency.SHA256; Note = 'Canonical Managed forwarding facade used instead.' }
                continue
            }
            if ($destinations[$leaf] -ne $dependency.SHA256) { throw "Conflicting dependency identities: $leaf" }
            continue
        }
        $destination = Join-Path $runRoot $leaf
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $dependency.SHA256) { throw "Input differs from compile evidence: $source" }
        Copy-Item -LiteralPath $source -Destination $destination
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $dependency.SHA256) { throw "Dependency copy changed: $source" }
        $destinations[$leaf] = $dependency.SHA256
        $manifest += [pscustomobject]@{ SourcePath = $source; RuntimePath = $destination; SHA256 = $dependency.SHA256 }
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runRoot 'dependency-manifest.json') -Encoding UTF8
    $sourceFiles = @('ManagedPureRunner.cs', 'PureTestPolicy.cs')
    if ($SelfTest) { $sourceFiles += 'PureTestSelfTests.cs' }
    foreach ($source in $sourceFiles) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $source) -Destination (Join-Path $runRoot $source) }
    $harnessSources = @($sourceFiles | ForEach-Object {
        [pscustomobject]@{ File = $_; SHA256 = (Get-FileHash -LiteralPath (Join-Path $runRoot $_) -Algorithm SHA256).Hash }
    })
    $harnessSources | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runRoot 'harness-source-manifest.json') -Encoding UTF8
    $frameworkRefs = @(Get-ChildItem -LiteralPath $runtime.FullName -Filter '*.dll' | Where-Object {
        try { $null = [Reflection.AssemblyName]::GetAssemblyName($_.FullName); $true }
        catch [BadImageFormatException] { $false }
    } | ForEach-Object { '-r:"' + $_.FullName + '"' })
    $common = @('-nologo', '-nostdlib+', '-langversion:9.0', '-deterministic+', '-warnaserror+') + $frameworkRefs +
        ('-r:"' + (Join-Path $runRoot 'nunit.framework.dll') + '"')
    $harness = Join-Path $runRoot 'ManagedPureRunner.dll'
    $rsp = Join-Path $runRoot 'ManagedPureRunner.rsp'
    $common + @('-target:exe', ('-out:"' + $harness + '"'), ('"' + (Join-Path $runRoot 'ManagedPureRunner.cs') + '"'),
        ('"' + (Join-Path $runRoot 'PureTestPolicy.cs') + '"')) | Set-Content -LiteralPath $rsp -Encoding UTF8
    $buildOutput = @(& $dotnet $compiler '-noconfig' ('@' + $rsp) 2>&1)
    $buildExit = $LASTEXITCODE
    $buildOutput | Set-Content -LiteralPath (Join-Path $runRoot 'compile.log') -Encoding UTF8
    if ($buildExit -ne 0) { $buildOutput | Write-Output; throw 'Harness compilation failed.' }
    @{ runtimeOptions = @{ tfm = ('net' + $runtime.Name.Split('.')[0] + '.0'); framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime.Name } } } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'ManagedPureRunner.runtimeconfig.json') -Encoding UTF8
    $assembly = Join-Path $runRoot 'Worsen.Tests.dll'
    if ($SelfTest) {
        $assembly = Join-Path $runRoot 'PureTestSelfTests.dll'
        $selfRsp = Join-Path $runRoot 'PureTestSelfTests.rsp'
        $common + @('-target:library', ('-out:"' + $assembly + '"'), ('-r:"' + $harness + '"'),
            ('-r:"' + (Join-Path $runRoot 'UnityEngine.CoreModule.dll') + '"'),
            ('-r:"' + (Join-Path $runRoot 'UnityEditor.CoreModule.dll') + '"'),
            ('-r:"' + (Join-Path $runRoot 'UnityEngine.TestRunner.dll') + '"'),
            ('"' + (Join-Path $runRoot 'PureTestSelfTests.cs') + '"')) | Set-Content -LiteralPath $selfRsp -Encoding UTF8
        $selfOutput = @(& $dotnet $compiler '-noconfig' ('@' + $selfRsp) 2>&1)
        $selfExit = $LASTEXITCODE
        $selfOutput | Set-Content -LiteralPath (Join-Path $runRoot 'self-compile.log') -Encoding UTF8
        if ($selfExit -ne 0) { $selfOutput | Write-Output; throw 'Self-test compilation failed.' }
    }
    $summaryPath = Join-Path $runRoot 'summary.json'
    # Windows PowerShell 5.1 drops empty native arguments; encode the filter.
    $filterArgument = 'filter:' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Filter))
    $arguments = @($harness, 'run', $assembly, $filterArgument, [string][int]($TimeoutSeconds * 1000), $dotnet, $summaryPath)
    @{ Executable = $dotnet; Arguments = $arguments } | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $runRoot 'command.json') -Encoding UTF8
    # Unity runs Edit Mode tests with the project root as the working directory, and some
    # fixtures read project files by relative path; match that (all harness paths are absolute).
    Push-Location $Worktree
    try { $output = @(& $dotnet @arguments 2>&1); $runExit = $LASTEXITCODE }
    finally { Pop-Location }
    $output | Set-Content -LiteralPath (Join-Path $runRoot 'results.log') -Encoding UTF8
    if (-not (Test-Path -LiteralPath $summaryPath)) { $output | Write-Output; throw 'No complete harness summary.' }
    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    $summary | Add-Member -NotePropertyName CompileRun -NotePropertyValue $CompileRun
    $summary | Add-Member -NotePropertyName TestAssemblySHA256 -NotePropertyValue (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
    $summary | Add-Member -NotePropertyName TotalWallSeconds -NotePropertyValue $wall.Elapsed.TotalSeconds
    $summary | Add-Member -NotePropertyName SelfTest -NotePropertyValue ([bool]$SelfTest)
    $summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
    $output | Write-Output
    exit $runExit
}
catch {
    $message = "HARNESS_ERROR $($_.Exception.Message)`n$($_.ScriptStackTrace)"
    if ($ownsRun -and (Test-Path -LiteralPath $runRoot)) {
        $message | Add-Content -LiteralPath (Join-Path $runRoot 'results.log') -Encoding UTF8
        if (-not (Test-Path -LiteralPath (Join-Path $runRoot 'summary.json'))) {
            @{ ExitCode = 2; CompileRun = $CompileRun; InfrastructureErrors = @($message); TotalWallSeconds = $wall.Elapsed.TotalSeconds } |
                ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'summary.json') -Encoding UTF8
        }
    }
    Write-Output $message
    exit 2
}
