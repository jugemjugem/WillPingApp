# IPv4 / IPv6 network monitor
$ErrorActionPreference = "Continue"
$intervalSeconds = 60
$pingCount = 4
$timeoutMs = 1200

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir = Join-Path $root "NetworkMonitorLogs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = Join-Path $logDir "monitor_$stamp.txt"
$csvFile = Join-Path $logDir "monitor_$stamp.csv"

function Write-Log([string]$Message) {
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')  $Message"
    Add-Content -LiteralPath $logFile -Value $line -Encoding UTF8
    Write-Host $line
}

function Test-PingTarget([string]$Label, [string]$Target, [string]$Family) {
    $args = @()
    if ($Family -eq "IPv4") { $args += "-4" } else { $args += "-6" }
    $args += @("-n", "$pingCount", "-w", "$timeoutMs", $Target)
    $output = & ping.exe @args 2>&1 | Out-String
    $sent = $pingCount
    $received = 0
    if ($output -match 'Sent\s*=\s*(\d+)') { $sent = [int]$Matches[1] }
    if ($output -match 'Received\s*=\s*(\d+)') { $received = [int]$Matches[1] }
    elseif ($output -match '受信\s*=\s*(\d+)') { $received = [int]$Matches[1] }
    $ok = ($received -gt 0)
    $status = if ($ok) { "OK" } else { "FAIL" }

    Add-Content -LiteralPath $logFile -Value "`r`n[$Label] $Family $Target`r`n$output" -Encoding UTF8
    return [pscustomobject]@{
        Label = $Label; Target = $Target; Family = $Family
        Sent = $sent; Received = $received; Lost = [Math]::Max(0, $sent - $received)
        Status = $status
    }
}

function Save-Diagnostic([string]$Reason) {
    $diag = Join-Path $logDir ("diagnostic_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".txt")
    "Diagnostic time: $(Get-Date -Format o)`r`nReason: $Reason`r`nComputer: $env:COMPUTERNAME" | Out-File $diag -Encoding utf8
    "`r`n===== ipconfig /all =====" | Out-File $diag -Append -Encoding utf8
    ipconfig /all | Out-File $diag -Append -Encoding utf8
    "`r`n===== route print -4 =====" | Out-File $diag -Append -Encoding utf8
    route.exe print -4 | Out-File $diag -Append -Encoding utf8
    "`r`n===== Get-NetAdapter =====" | Out-File $diag -Append -Encoding utf8
    Get-NetAdapter | Format-Table -AutoSize | Out-String | Out-File $diag -Append -Encoding utf8
    "`r`n===== DNS cache (display only) =====" | Out-File $diag -Append -Encoding utf8
    ipconfig /displaydns | Select-Object -First 100 | Out-File $diag -Append -Encoding utf8
    Write-Log "DIAGNOSTIC SAVED: $diag"
}

# Default gateway detection
$gateway = $null
try {
    $gateway = Get-NetRoute -AddressFamily IPv4 -DestinationPrefix "0.0.0.0/0" |
        Where-Object { $_.NextHop -and $_.NextHop -ne "0.0.0.0" } |
        Sort-Object RouteMetric, InterfaceMetric |
        Select-Object -First 1 -ExpandProperty NextHop
} catch {}
if (-not $gateway) { $gateway = "192.168.1.1" }

"Time,Gateway,IPv4_IP,IPv4_DNS,IPv6_DNS" | Out-File $csvFile -Encoding utf8
Write-Log "Monitor started. Interval=${intervalSeconds}s; gateway=$gateway; log=$logFile"
Write-Log "Each target sends $pingCount pings. Press any key in this window to stop."
Write-Log "Note: any reply counts as reachable; packet loss is recorded in the text log."

$inIncident = $false
$incidentStart = $null
$stopRequested = $false

while ($true) {
    if ([Console]::KeyAvailable) {
        [void][Console]::ReadKey($true)
        break
    }

    $time = Get-Date
    Write-Log "----- TEST START -----"

    $results = @()
    $results += Test-PingTarget "Router/default gateway" $gateway "IPv4"
    $results += Test-PingTarget "Public IPv4 address" "8.8.8.8" "IPv4"
    $results += Test-PingTarget "IPv4 DNS + reachability" "google.com" "IPv4"
    $results += Test-PingTarget "IPv6 DNS + reachability" "google.com" "IPv6"

    foreach ($r in $results) {
        Write-Log ("{0}: {1} {2}/{3} received, loss={4}" -f $r.Label, $r.Status, $r.Received, $r.Sent, $r.Lost)
    }

    $gw = ($results | Where-Object Label -eq "Router/default gateway").Status
    $ip4 = ($results | Where-Object Label -eq "Public IPv4 address").Status
    $dns4 = ($results | Where-Object Label -eq "IPv4 DNS + reachability").Status
    $dns6 = ($results | Where-Object Label -eq "IPv6 DNS + reachability").Status
    ('"{0}",{1},{2},{3},{4}' -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $gw, $ip4, $dns4, $dns6) |
        Add-Content -LiteralPath $csvFile -Encoding UTF8

    $failed = @($results | Where-Object Status -eq "FAIL")
    if ($failed.Count -gt 0) {
        $reason = ($failed | ForEach-Object { "$($_.Label)=$($_.Status)" }) -join "; "
        if (-not $inIncident) {
            $inIncident = $true
            $incidentStart = Get-Date
            Write-Log "!!! INCIDENT START: $reason"
            Save-Diagnostic $reason
        } else {
            Write-Log "!!! STILL FAILING: $reason"
        }
    } elseif ($inIncident) {
        $duration = (Get-Date) - $incidentStart
        Write-Log ("*** RECOVERED at {0}; estimated incident duration {1}" -f (Get-Date -Format "HH:mm:ss"), $duration)
        $inIncident = $false
        $incidentStart = $null
    } else {
        Write-Log "All targets responding."
    }

    # Wait in short increments so a keypress stops promptly.
    for ($i = 0; $i -lt $intervalSeconds * 4; $i++) {
        if ([Console]::KeyAvailable) {
            [void][Console]::ReadKey($true)
            $stopRequested = $true
            break
        }
        Start-Sleep -Milliseconds 250
    }
    if ($stopRequested) { break }
}
Write-Log "Monitor stopped by keyboard."
