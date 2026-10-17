param([string]$Output='.\WillOutput-Endings',[switch]$Render)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    if($Render){& .\Build-And-Export.ps1 -Scale 1 -MaxLoopSeconds 1 -Output $Output -Force}
    & .\WillActorExporter.exe --self-test
    if($LASTEXITCODE -ne 0){throw 'Logic tests failed'}
    $root=(Resolve-Path -LiteralPath $Output).Path
    $rows=@(Import-Csv -LiteralPath (Join-Path $root 'actions.csv'))
    if($rows.Count -ne 35){throw 'Expected 35 Will actions'}
    $expected=@{22='16,71';23='29,92';101='59,78';112='30,31'}
    foreach($r in $rows){
        if($r.classification -eq 'finite' -and $r.outcome -ne 'complete'){throw 'Finite action changed'}
        if($expected.ContainsKey([int]$r.action_id)){
            $p=Import-Csv -LiteralPath (Join-Path (Join-Path $root $r.folder) 'playback.csv')
            if($p.outcome -ne 'controlled_ending' -or $p.requested_cycle_passes -ne '3' -or ($p.forced_from_instruction+','+$p.forced_to_instruction) -ne $expected[[int]$r.action_id]){throw "Ending mismatch: $($r.action_id)"}
            $frames=@(Import-Csv -LiteralPath (Join-Path (Join-Path $root $r.folder) 'frames.csv'))
            if($frames[-1].pose_id -ne '160'){throw 'Action did not return to neutral'}
        }
    }
    foreach($dir in Get-ChildItem -LiteralPath $root -Directory | Where-Object { $_.Name -match '^\d{3,}_' }){
        if(-not (Test-Path (Join-Path $dir.FullName 'opcodes.csv'))){continue}
        $ops=@(Import-Csv -LiteralPath (Join-Path $dir.FullName 'opcodes.csv'))
        $path=@(Import-Csv -LiteralPath (Join-Path $dir.FullName 'instructions.csv'))
        $p=Import-Csv -LiteralPath (Join-Path $dir.FullName 'playback.csv')
        $overrides=0
        for($i=0;$i -lt $path.Count-1;$i++) {
            $pc=[int]$path[$i].instruction;$q=[int]$path[$i+1].instruction;$o=$ops[$pc]
            $allowed=if($o.opcode -eq '1'){([int]$o.operand_b -gt 0 -and $q -eq [int]$o.operand_a) -or ([int]$o.operand_b -lt 32767 -and $q -eq $pc+1)}else{$q -eq $pc+1}
            if(-not $allowed){
                if($i -ne [int]$p.forced_step -or $pc -ne [int]$p.forced_from_instruction -or $q -ne [int]$p.forced_to_instruction){throw "Undocumented jump: $($dir.Name) at $i"}
                $overrides++
            }
        }
        if($p.outcome -eq 'controlled_ending' -and $overrides -ne 1){throw 'Expected exactly one documented exit override'}
        $frames=@(Import-Csv -LiteralPath (Join-Path $dir.FullName 'frames.csv'))
        $offset=0L;$frame=0;$sounds=New-Object 'Collections.Generic.List[string]'
        foreach($step in $path){$o=$ops[[int]$step.instruction];if($o.opcode -eq '0' -and $o.operand_a -ne '65535'){
            $ms=[Math]::Max(1,[int]$o.operand_b)
            if([long]$frames[$frame].start_ms -ne $offset -or [int]$frames[$frame].duration_ms -ne $ms -or $frames[$frame].pose_id -ne $o.operand_a){throw 'Frame timeline mismatch'}
            $offset+=$ms;$frame++
        };if($o.opcode -eq '2' -and $o.operand_a -ne '65535'){$sounds.Add($step.instruction+','+$offset+','+$o.operand_a)}}
        $cues=@(Import-Csv -LiteralPath (Join-Path $dir.FullName 'sound_events.csv'))
        if($cues.Count -ne $sounds.Count){throw 'Sound trigger count mismatch'}
        for($i=0;$i -lt $cues.Count;$i++){if(($cues[$i].instruction+','+$cues[$i].start_ms+','+$cues[$i].sound_id) -ne $sounds[$i]){throw 'Sound timing mismatch'}}
    }
    $showcase=Join-Path $root '001_idle_showcase'
    $edges=@(Import-Csv -LiteralPath (Join-Path $showcase 'edges.csv'))
    if($edges.Count -ne 58 -or @($edges | Where-Object {$_.covered -ne 'yes'}).Count){throw 'Missed idle edge'}
    if(@(Get-ChildItem -LiteralPath (Join-Path $root 'sounds') -Filter *.wav).Count -ne 29){throw 'Missing WAVs'}
    $videos=@(Get-ChildItem -LiteralPath $root -Filter *.mp4)
    if($videos.Count -ne 36){throw 'Expected 36 MP4 files'}
    foreach($video in $videos){
        $probe=(& ffprobe -v error -show_streams -of json $video.FullName) -join "`n" | ConvertFrom-Json
        if($LASTEXITCODE -ne 0 -or -not ($probe.streams.codec_name -contains 'h264') -or -not ($probe.streams.codec_name -contains 'aac')){throw 'Bad media streams'}
        & ffmpeg -v error -i $video.FullName -f null -
        if($LASTEXITCODE -ne 0){throw 'MP4 decode failed'}
    }
    Write-Host 'PASS: 35 actions; all four deterministic loops use third-pass endings and return to neutral; exactly one documented override per ending; all frame/sound timestamps match instruction traces; 58/58 idle edges; 29 WAVs; 36 decodable H.264/AAC MP4s.'
} finally {Pop-Location}
