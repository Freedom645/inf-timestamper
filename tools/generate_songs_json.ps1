#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generate songs.json for INFINITAS from IIDX-Data-Table.

.DESCRIPTION
    Downloads textage/title.json and textage/chart-info.json from
    https://chinimuruhi.github.io/IIDX-Data-Table/ and produces
    src/InfTimestamper.Core/Resources/INFINITAS/songs.json by selecting
    songs whose chart-info has in_inf == true.

    Schema follows the "Song fuzzy match" section in docs/要件.md.
    Re-run this script when INFINITAS receives new songs.
#>

[CmdletBinding()]
param(
    [string] $TitleUrl     = "https://chinimuruhi.github.io/IIDX-Data-Table/textage/title.json",
    [string] $ChartInfoUrl = "https://chinimuruhi.github.io/IIDX-Data-Table/textage/chart-info.json",
    [string] $OutputPath
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrEmpty($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot "..\src\InfTimestamper.Core\Resources\INFINITAS\songs.json"
}

function ConvertTo-NormalizedTitle {
    param([Parameter(Mandatory=$true)][string] $Title)

    $upper = $Title.ToUpperInvariant()
    $sb = [System.Text.StringBuilder]::new()
    foreach ($ch in $upper.ToCharArray()) {
        $code = [int]$ch
        # Fullwidth ASCII (U+FF01 - U+FF5E) -> halfwidth
        if ($code -ge 0xFF01 -and $code -le 0xFF5E) {
            $code = $code - 0xFEE0
        }
        $c = [char]$code

        # Keep alphanumerics / hiragana / katakana / CJK; drop whitespace and symbols
        $isAscii  = ($code -ge 0x30 -and $code -le 0x39) -or
                    ($code -ge 0x41 -and $code -le 0x5A)
        $isKana   = ($code -ge 0x3041 -and $code -le 0x30FA)
        $isCjk    = ($code -ge 0x4E00 -and $code -le 0x9FFF)
        $isExtKa  = ($code -ge 0x31F0 -and $code -le 0x31FF)
        if ($isAscii -or $isKana -or $isCjk -or $isExtKa) {
            [void]$sb.Append($c)
        }
    }
    return $sb.ToString()
}

Write-Host "Fetching title.json..." -ForegroundColor Cyan
$titles = Invoke-WebRequest -Uri $TitleUrl -UseBasicParsing | Select-Object -ExpandProperty Content | ConvertFrom-Json

Write-Host "Fetching chart-info.json..." -ForegroundColor Cyan
$charts = Invoke-WebRequest -Uri $ChartInfoUrl -UseBasicParsing | Select-Object -ExpandProperty Content | ConvertFrom-Json

Write-Host "Extracting INFINITAS songs..." -ForegroundColor Cyan
$records = New-Object System.Collections.Generic.List[object]

foreach ($prop in $charts.PSObject.Properties) {
    $id = $prop.Name
    $info = $prop.Value
    if (-not $info.in_inf) { continue }
    $title = $titles.$id
    if (-not $title) { continue }

    $spLevels = @($info.level.sp)
    $dpLevels = @($info.level.dp)

    $chartsObj = [ordered]@{
        SPB = [int]$spLevels[0]
        SPN = [int]$spLevels[1]
        SPH = [int]$spLevels[2]
        SPA = [int]$spLevels[3]
        SPL = [int]$spLevels[4]
        DPB = [int]$dpLevels[0]
        DPN = [int]$dpLevels[1]
        DPH = [int]$dpLevels[2]
        DPA = [int]$dpLevels[3]
        DPL = [int]$dpLevels[4]
    }

    $records.Add([ordered]@{
        id               = $id
        title            = $title
        title_normalized = (ConvertTo-NormalizedTitle -Title $title)
        charts           = $chartsObj
    }) | Out-Null
}

$sortedArray = @($records | Sort-Object { [int]$_.id })
Write-Host "Extracted $($sortedArray.Count) songs." -ForegroundColor Green

$outDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

# WinPS 5.1 ConvertTo-Json uses 4-space indent. Re-indent to 2-space.
$json = ConvertTo-Json -InputObject $sortedArray -Depth 6
$lines = $json -split "`r?`n"
$normalized = New-Object System.Collections.Generic.List[string]
foreach ($line in $lines) {
    if ($line -match "^(\s+)(.*)$") {
        $indent = $matches[1]
        $rest   = $matches[2]
        $depth  = [int][Math]::Floor($indent.Length / 4)
        $extra  = $indent.Length % 4
        $normalized.Add(("  " * $depth) + (" " * $extra) + $rest)
    } else {
        $normalized.Add($line)
    }
}
$jsonText = [string]::Join("`n", $normalized) + "`n"

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$absoluteOutput = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.File]::WriteAllText($absoluteOutput, $jsonText, $utf8NoBom)

Write-Host "Wrote: $absoluteOutput" -ForegroundColor Green
