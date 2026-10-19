# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) CaptureImage Contributors
#
# Fails the CI `deadcode` job when JetBrains InspectCode reports dead or redundant
# code. Only file-local rules are gated: the `*.Global` variants produce false
# positives in a XAML + DI app (members used only from XAML bindings or resolved by
# the container), so they are listed in the job summary but never fail the build.

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Sarif,
    # Repository-relative path prefixes whose findings are ignored, separated by ';' or ','
    # (a single string, because `pwsh -File` does not parse arrays).
    [string] $ExcludePath = ''
)

$ErrorActionPreference = 'Stop'

$gatedRules = @(
    'UnusedMember.Local', 'UnusedType.Local', 'UnusedParameter.Local',
    'UnusedVariable', 'UnusedVariable.Compiler', 'NotAccessedVariable',
    'NotAccessedField.Local', 'UnusedField.Compiler', 'UnusedAutoPropertyAccessor.Local',
    'UnusedMethodReturnValue.Local', 'UnusedMemberInSuper.Local',
    'NotAccessedPositionalProperty.Local', 'EmptyNamespace',
    'RedundantUsingDirective', 'RedundantCast', 'RedundantSuppressNullableWarningExpression'
)
$reportedRules = @('UnusedMember.Global', 'UnusedType.Global', 'UnusedMethodReturnValue.Global')

$excluded = @($ExcludePath -split '[;,]' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$doc = Get-Content -Raw -LiteralPath $Sarif | ConvertFrom-Json
$results = @($doc.runs | ForEach-Object { $_.results })

$failures = @()
$informational = @()
foreach ($r in $results) {
    $loc = $r.locations[0].physicalLocation
    $path = $loc.artifactLocation.uri
    if ($excluded | Where-Object { $path.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }) { continue }
    $entry = [pscustomobject]@{ Rule = $r.ruleId; Path = $path; Line = $loc.region.startLine; Message = $r.message.text }
    if ($gatedRules -contains $r.ruleId) { $failures += $entry }
    elseif ($reportedRules -contains $r.ruleId) { $informational += $entry }
}

$summary = @(
    '## InspectCode — dead code', '',
    "Gated findings: **$($failures.Count)** · informational (``*.Global``): $($informational.Count)", ''
)
if ($failures.Count -gt 0) {
    $summary += '| Rule | Location | Message |', '|---|---|---|'
    foreach ($f in $failures) {
        $summary += "| ``$($f.Rule)`` | ``$($f.Path):$($f.Line)`` | $($f.Message -replace '\|', '\|') |"
        Write-Output "::error file=$($f.Path),line=$($f.Line),title=$($f.Rule)::$($f.Message)"
    }
}
if ($env:GITHUB_STEP_SUMMARY) { $summary | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8 }
else { $summary | Write-Output }

if ($failures.Count -gt 0) { exit 1 }
