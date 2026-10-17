<#
.SYNOPSIS
    打鍵カウンタ（inf_daken_counter_obsw）のデータ配信 WebSocket を購読し、受信メッセージを受信時刻つきでダンプする。

.DESCRIPTION
    issue #13 の検知方式（無音区間からのプレイ開始/終了の復元）を実機で確かめるためのプローブ。
    1 メッセージ = 1 行の JSON Lines で書き出す。各行は次のどちらか。

      {"receivedAt":"2026-10-17T21:00:00.123+09:00","type":"graph","bytes":1234,"message":{...受信した JSON そのまま...}}
      {"receivedAt":"2026-10-17T21:00:00.000+09:00","event":"connected","endpoint":"ws://127.0.0.1:8767"}

    event は connected / disconnected / error。無音区間と切断を区別するために入れている。
    停止は Ctrl+C。1 行ごとにフラッシュするので、途中で止めても書けた分は残る。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\daken-counter-probe.ps1
    powershell -ExecutionPolicy Bypass -File .\daken-counter-probe.ps1 -HostName 192.168.1.10 -Port 8767
#>
param(
    [string]$HostName = '127.0.0.1',
    [int]$Port = 8767,
    [string]$OutFile = ("daken-probe_{0}.jsonl" -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
)

$ErrorActionPreference = 'Stop'

$endpoint = [Uri]"ws://${HostName}:${Port}"
$outPath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $OutFile))
$writer = New-Object System.IO.StreamWriter($outPath, $true, (New-Object System.Text.UTF8Encoding($false)))
$writer.AutoFlush = $true

function Now { (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss.fffzzz') }

function Write-Event([string]$name, [string]$detail) {
    $line = '{"receivedAt":"' + (Now) + '","event":"' + $name + '","endpoint":"' + $endpoint + '"'
    if ($detail) { $line += ',"detail":' + (ConvertTo-Json $detail -Compress) }
    $writer.WriteLine($line + '}')
    Write-Host ("{0}  [{1}] {2}" -f (Now), $name, $detail)
}

# 画面に出す 1 行要約。history_cursong は選曲画面で 10Hz 流れるので、曲が変わったときだけ出す
$lastCursong = $null
function Show-Summary([string]$receivedAt, [string]$type, [string]$json, [int]$bytes) {
    $summary = ''
    switch ($type) {
        'graph' {
            if ($json -match '"playcount":\s*(\d+)') { $summary = "playcount=$($Matches[1])" }
        }
        'today_updates' {
            if ($json -match '"title":\s*"((?:[^"\\]|\\.)*)"') { $summary = "first.title=$($Matches[1])" }
        }
        'history_cursong' {
            $key = if ($json -match '"title":\s*"((?:[^"\\]|\\.)*)"') { $Matches[1] } else { '(empty)' }
            if ($key -eq $script:lastCursong) { return }
            $script:lastCursong = $key
            $summary = "title=$key"
        }
    }
    Write-Host ("{0}  {1,-16} {2,8}B  {3}" -f $receivedAt, $type, $bytes, $summary)
}

Write-Host "出力先: $outPath"
Write-Host "接続先: $endpoint （Ctrl+C で停止）"

$buffer = New-Object byte[] (64 * 1024)
try {
    while ($true) {
        $socket = New-Object System.Net.WebSockets.ClientWebSocket
        try {
            $socket.ConnectAsync($endpoint, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
            Write-Event 'connected' $null

            while ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
                $stream = New-Object System.IO.MemoryStream
                do {
                    $task = $socket.ReceiveAsync((New-Object ArraySegment[byte] -ArgumentList @(, $buffer)), [Threading.CancellationToken]::None)
                    # 長時間ブロックすると Ctrl+C が効かないので、短い間隔で待ちを刻む（完了すれば即座に抜ける）
                    while (-not $task.Wait(250)) { }
                    $result = $task.Result
                    if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) { break }
                    $stream.Write($buffer, 0, $result.Count)
                } while (-not $result.EndOfMessage)

                # 受信時刻はメッセージを受け切った時点
                $receivedAt = Now
                if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) { break }

                $bytes = [int]$stream.Length
                $json = [System.Text.Encoding]::UTF8.GetString($stream.ToArray())
                $type = if ($json -match '^\s*\{\s*"type":\s*"([^"]+)"') { $Matches[1] } else { '?' }
                $writer.WriteLine('{"receivedAt":"' + $receivedAt + '","type":"' + $type + '","bytes":' + $bytes + ',"message":' + $json + '}')
                Show-Summary $receivedAt $type $json $bytes
            }
            Write-Event 'disconnected' $null
        }
        catch {
            Write-Event 'error' $_.Exception.GetBaseException().Message
        }
        finally {
            $socket.Dispose()
        }
        Start-Sleep -Seconds 3
    }
}
finally {
    $writer.Dispose()
    Write-Host "保存しました: $outPath"
}
