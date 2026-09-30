param(
    [Parameter(Mandatory = $true)][string]$SnapshotName,
    [Parameter(Mandatory = $true)][string]$RunName,
    [string]$ProjectRoot = (Get-Location).Path
)
$ErrorActionPreference = 'Stop'
foreach ($name in @($SnapshotName, $RunName)) {
    if ($name -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Names must be simple directory names.' }
}
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$evidenceRoot = Join-Path $ProjectRoot 'Logs/AgentValidation/PLAN-002/offline-compile'
$snapshotRoot = Join-Path $evidenceRoot $SnapshotName
$runRoot = Join-Path $evidenceRoot $RunName
if (Test-Path -LiteralPath $runRoot) { throw 'Refusing to overwrite managed test evidence.' }
New-Item -ItemType Directory -Path $runRoot | Out-Null
$unityData = 'C:/Program Files/Unity/Hub/Editor/6000.3.12f1/Editor/Data'
$dotnet = Join-Path $unityData 'NetCoreRuntime/dotnet.exe'
$compiler = Join-Path $unityData 'DotNetSdkRoslyn/csc.dll'
$runtimeDirectory = Get-ChildItem -LiteralPath (Join-Path $unityData 'NetCoreRuntime/shared/Microsoft.NETCore.App') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$deps = @(Get-ChildItem -LiteralPath (Join-Path $snapshotRoot 'bin') -Filter '*.dll' | Select-Object -ExpandProperty FullName)
$deps += Join-Path $unityData 'Managed/UnityEngine.dll'
$deps += Join-Path $unityData 'Managed/UnityEngine/UnityEngine.CoreModule.dll'
$deps += @(Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'Library/PackageCache') -Recurse -File -Filter nunit.framework.dll | Select-Object -ExpandProperty FullName)
$dependencyManifest = @()
foreach ($dependency in $deps) {
    $destination = Join-Path $runRoot (Split-Path -Leaf $dependency)
    Copy-Item -LiteralPath $dependency -Destination $destination
    $sourceHash = (Get-FileHash -LiteralPath $dependency -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $sourceHash) { throw 'Managed dependency copy integrity failure.' }
    $dependencyManifest += [pscustomobject]@{ SourcePath = $dependency; RuntimePath = $destination; SHA256 = $sourceHash }
}
$dependencyManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runRoot 'dependency-manifest.json') -Encoding UTF8
$sourcePath = Join-Path $runRoot 'ManagedPureRunner.cs'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ManagedPureRunner.cs') -Destination $sourcePath
$outputDll = Join-Path $runRoot 'ManagedPureRunner.dll'
$responsePath = Join-Path $runRoot 'ManagedPureRunner.rsp'
$frameworkRefs = @(Get-Content -LiteralPath (Join-Path $snapshotRoot 'Worsen.Core.rsp') | Where-Object { $_ -match '^-r:.*NetStandard[/\\]' })
@('-nologo', '-nostdlib+', '-target:exe', '-langversion:9.0', ('-out:"' + $outputDll + '"')) + $frameworkRefs + ('"' + $sourcePath + '"') |
    Set-Content -LiteralPath $responsePath -Encoding UTF8
& $dotnet $compiler '-noconfig' ('@' + $responsePath) 2>&1 | Set-Content -LiteralPath (Join-Path $runRoot 'compile.log') -Encoding UTF8
if ($LASTEXITCODE -ne 0) { throw 'Managed harness compilation failed; see compile.log.' }
@{ runtimeOptions = @{ tfm = 'net6.0'; framework = @{ name = 'Microsoft.NETCore.App'; version = $runtimeDirectory.Name } } } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'ManagedPureRunner.runtimeconfig.json') -Encoding UTF8
$command = '& "' + $dotnet + '" "' + $outputDll + '"'
$command | Set-Content -LiteralPath (Join-Path $runRoot 'command.txt') -Encoding UTF8
$output = @(& $dotnet $outputDll 2>&1)
$runExitCode = $LASTEXITCODE
$output | Set-Content -LiteralPath (Join-Path $runRoot 'results.log') -Encoding UTF8
$resultLine = @($output | Where-Object { $_ -match '^MANAGED_RESULT passed=(\d+) failed=(\d+) environment_failures=(\d+) excluded_engine_cases=(\d+)$' })
if ($resultLine.Count -ne 1) { $output | Write-Output; throw 'Managed test run did not produce a complete result.' }
$null = $resultLine[0] -match '^MANAGED_RESULT passed=(\d+) failed=(\d+) environment_failures=(\d+) excluded_engine_cases=(\d+)$'
$summary = [pscustomobject]@{
    Scope = 'Executed selected managed NUnit methods by reflection; not Unity Test Framework execution.'
    Snapshot = $SnapshotName; Command = $command; ExitCode = $runExitCode
    Passed = [int]$Matches[1]; Failed = [int]$Matches[2]; EnvironmentFailures = [int]$Matches[3]
    ExcludedEngineCases = [int]$Matches[4]
    TestAssemblySHA256 = (Get-FileHash -LiteralPath (Join-Path $runRoot 'Worsen.Tests.dll') -Algorithm SHA256).Hash
    Fixtures = @('Worsen.Tests.Level.LevelGraphUtilityTests', 'Worsen.Tests.Level.LevelControllerTests', 'Worsen.Tests.Player.PlayerMoverPresenterTests', 'Worsen.Tests.Run.RunSessionControllerTests', 'Worsen.Tests.SceneFlow.SceneFlowControllerTests')
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'summary.json') -Encoding UTF8
$output | Write-Output
exit $runExitCode
