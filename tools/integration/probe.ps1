param([Parameter(Mandatory = $true)][string]$Code)
# Read-only one-line C# probe through Synaptic run_csharp; prints the result or fails loudly
# (resultSet:false is an error, never an empty success). Mutations belong in unity-setup.ps1.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Common.ps1"
Invoke-UnityCsharp $Code
