param([Parameter(Mandatory=$true)][string]$Output,[switch]$WebP,[switch]$Force,[switch]$NoMP4,[switch]$DebugVariants)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'MediaAudio.ps1')
$ffmpeg=(Get-Command ffmpeg -ErrorAction Stop).Source
$culture=[Globalization.CultureInfo]::InvariantCulture
$outFull=(Resolve-Path -LiteralPath $Output).Path
$pcm=@{}
if(-not $NoMP4){
    $cache=Join-Path $outFull '.audio-cache';New-Item -ItemType Directory -Path $cache -Force | Out-Null
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $outFull 'sounds') -Filter *.wav){
        $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        $decoded=Join-Path $cache ($hash+'.pcm')
        if(-not (Test-Path -LiteralPath $decoded)){
            & $ffmpeg -v error -y -i $file.FullName -vn -ar 48000 -ac 2 -f s16le $decoded
            if($LASTEXITCODE -ne 0){throw "Cannot decode sound: $($file.Name)"}
        }
        $pcm[$file.FullName]=[IO.File]::ReadAllBytes($decoded)
    }
}
Add-Type -AssemblyName System.Drawing
foreach($dir in Get-ChildItem -LiteralPath $outFull -Directory | Where-Object { $_.Name -match '^\d{3,}_' }){
    if(-not $DebugVariants -and $dir.Name -match '_variant_\d+$'){continue}
    $concat=Join-Path $dir.FullName 'animation.ffconcat'
    if(-not (Test-Path -LiteralPath (Join-Path $dir.FullName 'export.complete')) -or -not (Test-Path -LiteralPath $concat)){continue}
    $frames=@(Import-Csv -LiteralPath (Join-Path $dir.FullName 'frames.csv'));if(-not $frames.Count){continue}
    $events=@(Import-Csv -LiteralPath (Join-Path $dir.FullName 'sound_events.csv'))
    $videoMs=[long]$frames[-1].start_ms+[long]$frames[-1].duration_ms
    $target=Join-Path $outFull ($dir.Name+'.mp4')
    if(-not $NoMP4 -and ($Force -or -not (Test-Path -LiteralPath $target) -or (Get-Item -LiteralPath $concat).LastWriteTimeUtc -gt (Get-Item -LiteralPath $target).LastWriteTimeUtc)){
        $png=[IO.Path]::GetFullPath((Join-Path $dir.FullName $frames[0].file))
        $im=[Drawing.Image]::FromFile($png);$width=$im.Width;$height=$im.Height;$im.Dispose()
        $argsList=@('-hide_banner','-loglevel','error','-y','-safe','0','-f','concat','-i',$concat,'-f','lavfi','-i',"color=c=0x202020:s=$($width)x$($height):r=60")
        $duration=$videoMs/1000.0
        $filter='[1:v][0:v]overlay=eof_action=repeat:shortest=0,format=yuv420p[v];'
        if($events.Count){
            $audio=Join-Path $dir.FullName 'audio_timeline.wav'
            $duration=Write-ActorAudio $events $dir.FullName $pcm $videoMs $audio
            $argsList+=@('-i',$audio);$filter+='[2:a]apad[a]'
        }else{$filter+='anullsrc=r=48000:cl=stereo[a]'}
        $argsList+=@('-filter_complex',$filter,'-map','[v]','-map','[a]','-t',(($duration+0.5).ToString('0.000000',$culture)),'-r','60','-c:v','libx264','-crf','18','-preset','fast','-c:a','aac','-b:a','192k','-movflags','+faststart',$target)
        & $ffmpeg @argsList
        if($LASTEXITCODE -ne 0){throw "MP4 failed: $($dir.Name)"}
        Write-Host "MP4: $($dir.Name) ($($events.Count) sound triggers)"
    }
    if($WebP){
        $target=Join-Path $outFull ($dir.Name+'.webp')
        if($Force -or -not (Test-Path -LiteralPath $target) -or (Get-Item -LiteralPath $concat).LastWriteTimeUtc -gt (Get-Item -LiteralPath $target).LastWriteTimeUtc){
            $loop=if((Get-Content -LiteralPath (Join-Path $dir.FullName 'loop.txt')) -contains 'classification=complete'){1}else{0}
            & $ffmpeg -hide_banner -loglevel error -y -safe 0 -f concat -i $concat -fps_mode vfr -c:v libwebp_anim -lossless 1 -loop $loop $target
            if($LASTEXITCODE -ne 0){throw "WebP failed: $($dir.Name)"}
        }
    }
}
