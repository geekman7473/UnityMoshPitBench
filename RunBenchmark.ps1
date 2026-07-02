<#
.SYNOPSIS
    Runs all four Unity MoshPit Benchmark variants 5 times each and outputs CSV results.
.DESCRIPTION
    Executes Mono x64, IL2CPP x64, Mono ARM64, and IL2CPP ARM64 builds,
    parses the "Overall Score" from stdout, and writes a CSV with one row per run.
#>

param(
    [int]$Runs = 5
)

$ErrorActionPreference = 'Stop'

$rootDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$variants = [ordered]@{
    'Mono x64'      = Join-Path $rootDir 'BuildMono\UnityMoshPitBench.exe'
    'IL2CPP x64'    = Join-Path $rootDir 'BuildIl2cpp\UnityMoshPitBench.exe'
    'Mono ARM64'    = Join-Path $rootDir 'BuildMonoARM64\UnityMoshPitBench.exe'
    'IL2CPP ARM64'  = Join-Path $rootDir 'BuildIl2cppARM64\UnityMoshPitBench.exe'
}

# Verify all executables exist
foreach ($kv in $variants.GetEnumerator()) {
    if (-not (Test-Path $kv.Value)) {
        Write-Error "Executable not found: $($kv.Value)"
        exit 1
    }
}

# results[variant][runIndex] = score
$results = @{}
foreach ($name in $variants.Keys) {
    $results[$name] = @($null) * $Runs
}

function Get-OverallScore {
    param([string[]]$Output)
    foreach ($line in $Output) {
        if ($line -match 'Overall Score:\s+([\d.]+)\s+FPS') {
            return [double]$Matches[1]
        }
    }
    return $null
}

$totalRuns = $variants.Count * $Runs
$completed = 0

foreach ($kv in $variants.GetEnumerator()) {
    $name = $kv.Key
    $exe  = $kv.Value
    $workDir = Split-Path -Parent $exe

    for ($i = 0; $i -lt $Runs; $i++) {
        $completed++
        Write-Host "[$completed/$totalRuns] Running $name (run $($i+1)/$Runs)..." -ForegroundColor Cyan

        try {
            $output = & $exe -logfile - 2>$null | Out-String -Stream
            $score = Get-OverallScore -Output $output

            if ($null -eq $score) {
                Write-Warning "  Could not parse score from output. Storing N/A."
                $results[$name][$i] = 'N/A'
            } else {
                Write-Host "  Score: $score FPS" -ForegroundColor Green
                $results[$name][$i] = $score
            }
        }
        catch {
            Write-Warning "  Run failed: $_"
            $results[$name][$i] = 'ERROR'
        }
    }
}

# Build CSV
$csvPath = Join-Path $rootDir "BenchmarkResults.csv"
$header = "Run," + (($variants.Keys | ForEach-Object { $_ }) -join ',')
$lines = @($header)

for ($i = 0; $i -lt $Runs; $i++) {
    $row = "$($i+1)"
    foreach ($name in $variants.Keys) {
        $row += ",$($results[$name][$i])"
    }
    $lines += $row
}

# Averages row
$avgRow = "Avg"
foreach ($name in $variants.Keys) {
    $scores = $results[$name] | Where-Object { $_ -is [double] }
    if ($scores.Count -gt 0) {
        $avg = ($scores | Measure-Object -Average).Average
        $avgRow += ",$([math]::Round($avg, 1))"
    } else {
        $avgRow += ",N/A"
    }
}
$lines += $avgRow

$csv = $lines -join "`n"

# Write to file and stdout
$csv | Out-File -FilePath $csvPath -Encoding utf8 -NoNewline
Write-Host ""
Write-Host "Results saved to: $csvPath" -ForegroundColor Yellow
Write-Host ""
Write-Host $csv
