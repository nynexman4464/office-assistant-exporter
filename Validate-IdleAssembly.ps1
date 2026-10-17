function Test-IdleAssembly([string]$Dir,[switch]$Media){
    $root=Split-Path $Dir -Parent
    $segments=@(Import-Csv -LiteralPath (Join-Path $Dir 'segments.csv'))
    $frames=@(Import-Csv -LiteralPath (Join-Path $Dir 'frames.csv'))
    $sounds=@(Import-Csv -LiteralPath (Join-Path $Dir 'sound_events.csv'))
    $frame=0;$sound=0;$time=0L
    foreach($segment in $segments){
        if([int]$segment.start_frame -ne $frame -or [long]$segment.start_ms -ne $time){throw 'Assembly segment offset mismatch'}
        $source=Join-Path $root $segment.variant;$start=$time
        $body=@(Import-Csv -LiteralPath (Join-Path $source 'frames.csv'))
        if($body.Count -ne [int]$segment.body_frames){throw 'Assembly body count mismatch'}
        foreach($f in $body){
            $merged=$frames[$frame]
            if([long]$merged.start_ms -ne $time -or $merged.pose_id -ne $f.pose_id -or $merged.duration_ms -ne $f.duration_ms -or $merged.file -ne ('../'+$segment.variant+'/'+$f.file)){throw 'Assembly changed source frame/timing'}
            $time+=[long]$f.duration_ms;$frame++
        }
        foreach($cue in @(Import-Csv -LiteralPath (Join-Path $source 'sound_events.csv'))){
            $merged=$sounds[$sound]
            if([long]$merged.start_ms -ne $start+[long]$cue.start_ms -or $merged.sound_id -ne $cue.sound_id -or $merged.instruction -ne $cue.instruction -or $merged.source_variant -ne $segment.variant){throw 'Assembly changed source sound timing'}
            $sound++
        }
        if($time-$start -ne [long]$segment.body_duration_ms){throw 'Assembly body duration mismatch'}
        $rest=$frames[$frame]
        if($rest.file -ne 'rest.png' -or $rest.pose_id -ne $segment.rest_pose -or $rest.duration_ms -ne '250' -or [long]$rest.start_ms -ne $time){throw 'Assembly rest/join mismatch'}
        $time+=250;$frame++
    }
    if($frame -ne $frames.Count -or $sound -ne $sounds.Count){throw 'Extra/missing assembled frames or sounds'}
    if($Media){
        if(-not (Test-Path -LiteralPath (Join-Path $root ((Split-Path $Dir -Leaf)+'.mp4')))){throw 'Assembled MP4 missing'}
        foreach($extension in @('.mp4','.webp')){
            $path=Join-Path $root ((Split-Path $Dir -Leaf)+$extension)
            if(Test-Path -LiteralPath $path){& ffmpeg -v error -i $path -fps_mode passthrough -enc_time_base 1/1000 -f null -;if($LASTEXITCODE -ne 0){throw 'Assembled media decode failed'}}
        }
    }
    Write-Host ('PASS assembled idle: '+(Split-Path $root -Leaf)+', '+$segments.Count+' valid sections, '+$frames.Count+' frames and '+$sounds.Count+' aligned sound cues.')
}
