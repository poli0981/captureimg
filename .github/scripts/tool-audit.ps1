# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) CaptureImage Contributors
#
# NuGet's audit only checks package references, and GitHub's dependency graph doesn't read
# .config/dotnet-tools.json, so nothing else watches the local tools (vpk, the Sonar scanner, ...).
# The CI `audit` job runs this script: it looks up every tool version in nuget.org's
# vulnerability data (the data NuGetAudit uses), fails on any finding (any severity) and lists
# them in the job summary.

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Manifest,
    [string] $ServiceIndex = 'https://api.nuget.org/v3/index.json'
)

$ErrorActionPreference = 'Stop'

# A NuGet version: up to four numbers, an optional pre-release label; build metadata is ignored.
function Split-NuGetVersion([string] $Version) {
    $release, $label = ($Version.Trim() -split '\+', 2)[0] -split '-', 2
    $parts = [long[]]($release -split '\.')
    $numbers = [long[]]::new(4)
    [Array]::Copy($parts, $numbers, $parts.Count)
    [pscustomobject]@{ Numbers = $numbers; Label = $label }
}

# -1, 0 or 1. A release sorts after its pre-releases; labels compare part by part (SemVer 2.0).
function Compare-NuGetVersion([string] $Left, [string] $Right) {
    $a = Split-NuGetVersion $Left
    $b = Split-NuGetVersion $Right
    for ($i = 0; $i -lt 4; $i++) {
        if ($a.Numbers[$i] -ne $b.Numbers[$i]) { return [Math]::Sign($a.Numbers[$i] - $b.Numbers[$i]) }
    }
    if ($a.Label -eq $b.Label) { return 0 }
    if (-not $a.Label) { return 1 }
    if (-not $b.Label) { return -1 }
    $x = $a.Label -split '\.'
    $y = $b.Label -split '\.'
    for ($i = 0; $i -lt [Math]::Min($x.Count, $y.Count); $i++) {
        $xIsNumber = $x[$i] -match '^\d+$'
        $yIsNumber = $y[$i] -match '^\d+$'
        $order = if ($xIsNumber -and $yIsNumber) { ([long]$x[$i]).CompareTo([long]$y[$i]) }
        elseif ($xIsNumber) { -1 }
        elseif ($yIsNumber) { 1 }
        else { [string]::Compare($x[$i], $y[$i], [StringComparison]::OrdinalIgnoreCase) }
        if ($order -ne 0) { return [Math]::Sign($order) }
    }
    return [Math]::Sign($x.Count - $y.Count)
}

# NuGet range notation: "1.0" (minimum, inclusive), "[1.0]" (exact), "[1.0, 2.0)", "(, 2.0]", ...
function Test-VersionInRange([string] $Version, [string] $Range) {
    $Range = $Range.Trim()
    if ($Range -notmatch '^[\[(]') { return (Compare-NuGetVersion $Version $Range) -ge 0 }
    $inner = $Range.Substring(1, $Range.Length - 2)
    if ($inner -notmatch ',') { return (Compare-NuGetVersion $Version $inner) -eq 0 }
    $min, $max = $inner -split ',', 2 | ForEach-Object { $_.Trim() }
    if ($min) {
        $order = Compare-NuGetVersion $Version $min
        if ($order -lt 0 -or ($order -eq 0 -and $Range[0] -eq '(')) { return $false }
    }
    if ($max) {
        $order = Compare-NuGetVersion $Version $max
        if ($order -gt 0 -or ($order -eq 0 -and $Range[-1] -eq ')')) { return $false }
    }
    return $true
}

$tools = @((Get-Content -Raw -LiteralPath $Manifest | ConvertFrom-Json).tools.PSObject.Properties)
$resource = (Invoke-RestMethod -Uri $ServiceIndex).resources |
    Where-Object { $_.'@type' -like 'VulnerabilityInfo/*' } | Select-Object -First 1
if (-not $resource) { throw "$ServiceIndex has no VulnerabilityInfo resource." }

$severities = 'Low', 'Moderate', 'High', 'Critical'
$rows = @()
# The data comes in pages (a base file plus updates), each keyed by lower-case package id.
# Invoke-RestMethod returns a JSON array as one object; `foreach` enumerates it.
$pages = Invoke-RestMethod -Uri $resource.'@id'
foreach ($page in $pages) {
    $advisories = Invoke-RestMethod -Uri $page.'@id'
    foreach ($tool in $tools) {
        foreach ($advisory in @($advisories.($tool.Name.ToLowerInvariant()))) {
            if (-not $advisory -or -not (Test-VersionInRange $tool.Value.version $advisory.versions)) { continue }
            $rows += [pscustomobject]@{
                Tool     = $tool.Name
                Version  = $tool.Value.version
                Severity = $severities[[int]$advisory.severity]
                Advisory = $advisory.url
            }
        }
    }
}

$rows = @($rows | Sort-Object Tool, Version, Advisory -Unique)
$summary = @('## Tool vulnerability audit', '', "Tools checked: **$($tools.Count)**, vulnerable: **$($rows.Count)**", '')
if ($rows.Count -gt 0) {
    $summary += '| Tool | Version | Severity | Advisory |', '|---|---|---|---|'
    foreach ($r in $rows) {
        $summary += "| $($r.Tool) | $($r.Version) | $($r.Severity) | $($r.Advisory) |"
        Write-Output "::error title=Vulnerable tool::$($r.Tool) $($r.Version) ($($r.Severity)) $($r.Advisory)"
    }
}
if ($env:GITHUB_STEP_SUMMARY) { $summary | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8 }
else { $summary | Write-Output }

if ($rows.Count -gt 0) { exit 1 }
