param(
    [string[]]$Plans = @('PLAN-002', 'PLAN-003', 'PLAN-004', 'PLAN-006', 'PLAN-010'),
    [hashtable]$OverlayPrefixes = @{},
    [hashtable]$OverlayRoots = @{},
    [ValidateSet('Core', 'Domain', 'Session', 'Presentation', 'Orchestrator', 'Editor', 'Tests')]
    [string]$Through = 'Tests',
    [string]$RunName = ('run-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [string]$ProjectRoot = (Get-Location).Path
)

# Offline Roslyn compilation only. This harness never invokes Unity, imports assets,
# executes tests, or writes outside its own validation directory.
$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$outputRoot = Join-Path $ProjectRoot 'Logs/AgentValidation/PLAN-002/offline-compile'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple directory name.' }
$runRoot = Join-Path $outputRoot $RunName
if (Test-Path -LiteralPath $runRoot) { throw "Refusing to reuse an existing run: $runRoot" }
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$snapshotRoot = Join-Path $runRoot 'snapshot'
$binRoot = Join-Path $runRoot 'bin'
New-Item -ItemType Directory -Path $snapshotRoot, $binRoot | Out-Null
$unityData = 'C:/Program Files/Unity/Hub/Editor/6000.3.12f1/Editor/Data'
$dotnet = Join-Path $unityData 'NetCoreRuntime/dotnet.exe'
$compiler = Join-Path $unityData 'DotNetSdkRoslyn/csc.dll'
foreach ($required in @($dotnet, $compiler)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Missing compiler input: $required" }
}

$layers = @('Core', 'Domain', 'Session', 'Presentation', 'Orchestrator', 'Editor', 'Tests')
$selectedLayers = $layers[0..([array]::IndexOf($layers, $Through))]
function Get-Layer([string]$relativePath) {
    if ($relativePath -match '^Assets/Scripts/([^/]+)/') { return $Matches[1] }
    if ($relativePath.StartsWith('Assets/Editor/Tests/')) { return 'Tests' }
    if ($relativePath.StartsWith('Assets/Editor/')) { return 'Editor' }
    return ''
}

$sourceMap = @{}
$overlayClaims = @{}
$roots = @([pscustomobject]@{ Root = $ProjectRoot; Owner = 'baseline' })
foreach ($plan in $Plans) {
    if ($plan -notmatch '^PLAN-\d{3}$') { throw "Invalid plan name: $plan" }
    $planStageRoot = Join-Path $ProjectRoot "Logs/AgentStaging/$plan"
    $stageCandidates = @($planStageRoot)
    if ($OverlayRoots.ContainsKey($plan)) {
        $stageCandidates = @($OverlayRoots[$plan])
    }
    $allowedRoot = [IO.Path]::GetFullPath($planStageRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    foreach ($stageCandidate in $stageCandidates) {
        $stageRoot = (Resolve-Path -LiteralPath $stageCandidate).Path
        if (-not $stageRoot.Equals($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -and
            -not $stageRoot.StartsWith($allowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Overlay root must remain within its owning plan staging directory: $stageRoot"
        }
        if (-not (Test-Path -LiteralPath (Join-Path $stageRoot 'Assets'))) {
            throw "Requested overlay is absent: $stageRoot/Assets"
        }
        $roots += [pscustomobject]@{ Root = $stageRoot; Owner = $plan }
    }
}
foreach ($root in $roots) {
    foreach ($subtree in @('Assets/Scripts', 'Assets/Editor')) {
        $scanRoot = Join-Path $root.Root $subtree
        if (-not (Test-Path -LiteralPath $scanRoot)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $scanRoot -Recurse -File) {
            if ($file.Extension -notin @('.cs', '.asmdef')) { continue }
            $relative = $file.FullName.Substring($root.Root.Length + 1).Replace('\', '/')
            $layer = Get-Layer $relative
            if ($layer -notin $selectedLayers) { continue }
            if ($root.Owner -ne 'baseline' -and $OverlayPrefixes.ContainsKey($root.Owner)) {
                $included = $false
                foreach ($prefix in $OverlayPrefixes[$root.Owner]) {
                    if ($relative.StartsWith($prefix)) { $included = $true; break }
                }
                if (-not $included) { continue }
            }
            if ($root.Owner -ne 'baseline') {
                if ($overlayClaims.ContainsKey($relative)) {
                    throw "Ownership conflict: $relative claimed by $($overlayClaims[$relative]) and $($root.Owner)"
                }
                $overlayClaims[$relative] = $root.Owner
            }
            $sourceMap[$relative] = [pscustomobject]@{
                RelativePath = $relative; SourcePath = $file.FullName; Owner = $root.Owner; Layer = $layer
            }
        }
    }
}

$manifest = @()
foreach ($relative in ($sourceMap.Keys | Sort-Object)) {
    $item = $sourceMap[$relative]
    $target = Join-Path $snapshotRoot $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    $before = (Get-FileHash -LiteralPath $item.SourcePath -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $item.SourcePath -Destination $target
    $snapshotHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    $after = (Get-FileHash -LiteralPath $item.SourcePath -Algorithm SHA256).Hash
    if ($before -ne $snapshotHash -or $after -ne $snapshotHash) {
        throw "Source changed during snapshot: $($item.SourcePath)"
    }
    $manifest += [pscustomobject]@{
        RelativePath = $relative; SourcePath = $item.SourcePath; SnapshotPath = $target
        Owner = $item.Owner; Layer = $item.Layer; SHA256 = $snapshotHash
    }
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'source-manifest.json') -Encoding UTF8

$frameworkRefs = @(
    Get-ChildItem -LiteralPath (Join-Path $unityData 'NetStandard/ref/2.1.0') -Filter '*.dll'
    Get-ChildItem -LiteralPath (Join-Path $unityData 'NetStandard/compat/2.1.0/shims/netstandard') -Filter '*.dll'
    Get-ChildItem -LiteralPath (Join-Path $unityData 'NetStandard/compat/2.1.0/shims/netfx') -Filter '*.dll'
) | Select-Object -ExpandProperty FullName
$engineRefs = @(Get-ChildItem -LiteralPath (Join-Path $unityData 'Managed/UnityEngine') -Filter '*.dll' | Select-Object -ExpandProperty FullName)
$engineRefs += Join-Path $unityData 'Managed/UnityEngine.dll'
$editorRefs = @(Get-ChildItem -LiteralPath (Join-Path $unityData 'Managed') -Filter 'UnityEditor*.dll' | Select-Object -ExpandProperty FullName)
$beeCoreRsp = Join-Path $ProjectRoot 'Library/Bee/artifacts/1900b0aE.dag/Worsen.Core.rsp'
if (-not (Test-Path -LiteralPath $beeCoreRsp)) { throw "Missing baseline define evidence: $beeCoreRsp" }
$defines = @(Get-Content -LiteralPath $beeCoreRsp | Where-Object { $_ -like '-define:*' })
$results = @()
$referenceManifest = @{}
$succeeded = @{}
foreach ($layer in $selectedLayers) {
    $assemblyName = "Worsen.$layer"
    $assemblyRecord = @($manifest | Where-Object { $_.Layer -eq $layer -and $_.RelativePath.EndsWith('.asmdef') })
    if ($assemblyRecord.Count -ne 1) { throw "Expected one asmdef for $layer, found $($assemblyRecord.Count)" }
    $asmdef = Get-Content -LiteralPath $assemblyRecord[0].SnapshotPath -Raw | ConvertFrom-Json
    $sourceFiles = @($manifest | Where-Object { $_.Layer -eq $layer -and $_.RelativePath.EndsWith('.cs') })
    $refs = @($frameworkRefs) + @($engineRefs)
    if ($layer -in @('Editor', 'Tests')) { $refs += $editorRefs }
    $missingPrerequisites = @()
    foreach ($reference in $asmdef.references) {
        if ($reference.StartsWith('Worsen.')) {
            if (-not $succeeded[$reference]) { $missingPrerequisites += $reference }
            $refs += Join-Path $binRoot ($reference + '.dll')
        } else {
            $packageRef = Join-Path $ProjectRoot ('Library/ScriptAssemblies/' + $reference + '.dll')
            if (-not (Test-Path -LiteralPath $packageRef)) { throw "Missing explicit package reference $reference" }
            $refs += $packageRef
        }
    }
    foreach ($precompiled in $asmdef.precompiledReferences) {
        $matches = @(Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'Library/PackageCache') -Recurse -File -Filter $precompiled)
        if ($matches.Count -ne 1) { throw "Expected one precompiled reference $precompiled, found $($matches.Count)" }
        $refs += $matches[0].FullName
    }
    if ($missingPrerequisites.Count -gt 0) {
        $results += [pscustomobject]@{ Assembly = $assemblyName; Status = 'BlockedByCompileFailure'; Missing = $missingPrerequisites; Sources = $sourceFiles.Count }
        continue
    }
    $refs = @($refs | Sort-Object -Unique)
    foreach ($referencePath in $refs) {
        if (-not $referenceManifest.ContainsKey($referencePath)) {
            $referenceManifest[$referencePath] = (Get-FileHash -LiteralPath $referencePath -Algorithm SHA256).Hash
        }
    }
    $outputDll = Join-Path $binRoot ($assemblyName + '.dll')
    $rspPath = Join-Path $runRoot ($assemblyName + '.rsp')
    $logPath = Join-Path $runRoot ($assemblyName + '.log')
    $rsp = @('-nologo', '-nostdlib+', '-target:library', '-langversion:9.0', '-utf8output', '-deterministic+', '-warn:4')
    $rsp += '-out:"' + $outputDll + '"'
    $rsp += $defines
    if ($layer -eq 'Tests') { $rsp += '-define:UNITY_INCLUDE_TESTS' }
    if ($asmdef.allowUnsafeCode) { $rsp += '-unsafe+' }
    $rsp += @($refs | ForEach-Object { '-r:"' + $_ + '"' })
    $rsp += @($sourceFiles | ForEach-Object { '"' + $_.SnapshotPath + '"' })
    $rsp | Set-Content -LiteralPath $rspPath -Encoding UTF8
    $command = '& "' + $dotnet + '" "' + $compiler + '" -noconfig "@' + $rspPath + '"'
    $command | Set-Content -LiteralPath (Join-Path $runRoot ($assemblyName + '.command.txt')) -Encoding UTF8
    $compilerOutput = @(& $dotnet $compiler '-noconfig' ('@' + $rspPath) 2>&1)
    $exitCode = $LASTEXITCODE
    $compilerOutput | Set-Content -LiteralPath $logPath -Encoding UTF8
    $succeeded[$assemblyName] = ($exitCode -eq 0)
    $errors = @($compilerOutput | Where-Object { $_ -match '\berror CS\d+' }).Count
    $warnings = @($compilerOutput | Where-Object { $_ -match '\bwarning CS\d+' }).Count
    $results += [pscustomobject]@{
        Assembly = $assemblyName; Status = $(if ($exitCode -eq 0) { 'Compiled' } else { 'CompileFailed' })
        Sources = $sourceFiles.Count; ExitCode = $exitCode; Errors = $errors; Warnings = $warnings
        References = $refs; Command = $command; Log = $logPath
    }
    Write-Output "$assemblyName`: exit=$exitCode sources=$($sourceFiles.Count) errors=$errors warnings=$warnings"
    if ($exitCode -ne 0) { $compilerOutput | Write-Output }
}
$referenceManifest.GetEnumerator() | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{ Path = $_.Name; SHA256 = $_.Value; StableAtEnd = ((Get-FileHash -LiteralPath $_.Name -Algorithm SHA256).Hash -eq $_.Value) }
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'reference-manifest.json') -Encoding UTF8
$sourceDrift = @($manifest | Where-Object { (Get-FileHash -LiteralPath $_.SourcePath -Algorithm SHA256).Hash -ne $_.SHA256 } | Select-Object RelativePath, Owner)
$summary = [pscustomobject]@{
    Scope = 'Offline C# compile only; no Unity import, test execution, scene validation, or native runtime behavior established.'
    CapturedUtc = [DateTime]::UtcNow.ToString('o'); Plans = $Plans; OverlayPrefixes = $OverlayPrefixes; Through = $Through
    Compiler = $compiler; CompilerSHA256 = (Get-FileHash -LiteralPath $compiler -Algorithm SHA256).Hash
    SnapshotFiles = $manifest.Count; SourceDriftAfterCapture = $sourceDrift; Results = $results
}
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runRoot 'summary.json') -Encoding UTF8
Write-Output "Evidence: $runRoot"
if (@($results | Where-Object { $_.Status -ne 'Compiled' }).Count -gt 0) { exit 1 }
