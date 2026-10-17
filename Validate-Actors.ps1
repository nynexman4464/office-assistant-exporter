param([Parameter(Mandatory=$true)][string]$Output,[switch]$DebugVariants)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Validate-IdleAssembly.ps1')
$root=(Resolve-Path -LiteralPath $Output).Path
$actors=0;$videos=0;$traces=0
foreach($actor in Get-ChildItem -LiteralPath $root -Directory) {
    $manifest=Join-Path $actor.FullName 'actions.csv'
    if(-not (Test-Path -LiteralPath $manifest)){continue}
    $actors++
    foreach($dir in Get-ChildItem -LiteralPath $actor.FullName -Directory | Where-Object { $_.Name -match '^\d{3,}_' }) {
        if(Test-Path -LiteralPath (Join-Path $dir.FullName 'segments.csv')){Test-IdleAssembly $dir.FullName -Media;$videos++;continue}
        if(-not (Test-Path -LiteralPath (Join-Path $dir.FullName 'opcodes.csv'))){continue}
        $ops=@(Import-Csv -LiteralPath (Join-Path $dir.FullName 'opcodes.csv'))
        $path=@(Import-Csv -LiteralPath (Join-Path $dir.FullName 'instructions.csv'))
        $playback=Import-Csv -LiteralPath (Join-Path $dir.FullName 'playback.csv')
        $frames=@(Import-Csv -LiteralPath (Join-Path $dir.FullName 'frames.csv'))
        $cues=@(Import-Csv -LiteralPath (Join-Path $dir.FullName 'sound_events.csv'))
        $time=0L;$frame=0;$sound=0;$forced=0
        for($i=0;$i -lt $path.Count;$i++) {
            $pc=[int]$path[$i].instruction;$o=$ops[$pc]
            if($o.opcode -eq '0' -and $o.operand_a -ne '65535'){
                $ms=[Math]::Max(1,[int]$o.operand_b)
                if($frame -ge $frames.Count -or [long]$frames[$frame].start_ms -ne $time -or [int]$frames[$frame].duration_ms -ne $ms -or $frames[$frame].pose_id -ne $o.operand_a){throw "Frame timing mismatch: $($actor.Name)/$($dir.Name)"}
                $frame++;$time+=$ms
            }
            if($o.opcode -eq '2' -and $o.operand_a -ne '65535'){
                if($sound -ge $cues.Count -or [long]$cues[$sound].start_ms -ne $time -or $cues[$sound].sound_id -ne $o.operand_a){throw "Sound timing mismatch: $($actor.Name)/$($dir.Name)"}
                $sound++
            }
            if($i+1 -lt $path.Count){
                $q=[int]$path[$i+1].instruction
                $valid=if($o.opcode -eq '1'){([int]$o.operand_b -gt 0 -and $q -eq [int]$o.operand_a) -or ([int]$o.operand_b -lt 32767 -and $q -eq $pc+1)}else{$q -eq $pc+1}
                if(-not $valid){
                    if($i -ne [int]$playback.forced_step -or $pc -ne [int]$playback.forced_from_instruction -or $q -ne [int]$playback.forced_to_instruction){throw "Undocumented transition: $($actor.Name)/$($dir.Name)"}
                    $forced++
                }
            }
        }
        if($frame -ne $frames.Count -or $sound -ne $cues.Count){throw 'Trace counts mismatch'}
        if($playback.outcome -eq 'controlled_ending' -and $forced -ne 1){throw 'Controlled ending must have one recorded override'}
        if($frames.Count -gt 0 -and ($DebugVariants -or $dir.Name -notmatch '_variant_\d+$')){
            $mp4=Join-Path $actor.FullName ($dir.Name+'.mp4')
            if(-not (Test-Path -LiteralPath $mp4)){throw "Missing MP4: $mp4"}
            & ffmpeg -v error -i $mp4 -f null -
            if($LASTEXITCODE -ne 0){throw "Failed decoding: $mp4"};$videos++
        }
        $traces++
    }
    foreach($edgeFile in @((Join-Path $actor.FullName '001_idle_showcase\edges.csv'),(Join-Path $actor.FullName 'idle_variant_coverage.csv'))){
        if(Test-Path -LiteralPath $edgeFile){$edges=@(Import-Csv -LiteralPath $edgeFile);if(@($edges | Where-Object {$_.covered -ne 'yes'}).Count){throw "Incomplete idle coverage: $($actor.Name)"}}
    }
    Write-Host ('PASS '+$actor.Name+': instruction transitions and frame/sound timing match; MP4s decode.')
}
Write-Host ('PASS: '+$actors+' actors, '+$traces+' traced exports, '+$videos+' decoded MP4s; documented exit overrides and complete reported idle coverage.')
