# Streaming, single-channel sound timeline. Original source WAVs remain intact.
function Write-ActorSilence($writer,[long]$bytes) {
    $zero=New-Object byte[] 65536
    while($bytes -gt 0){$count=[int][Math]::Min($zero.Length,$bytes);$writer.Write($zero,0,$count);$bytes-=$count}
}
function Write-ActorAudio([object[]]$events,[string]$dir,[hashtable]$pcm,[long]$videoMs,[string]$target) {
    $stream=[IO.File]::Open($target,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None)
    $writer=New-Object IO.BinaryWriter $stream
    try {
        $writer.Write((New-Object byte[] 44));$written=0L
        for($i=0;$i -lt $events.Count;$i++){
            $cue=$events[$i];$wav=[IO.Path]::GetFullPath((Join-Path $dir $cue.file))
            if(-not $pcm.ContainsKey($wav)){throw "Missing decoded WAV: $wav"}
            $data=$pcm[$wav];$start=[long]$cue.start_ms*192;$length=[long]$data.Length
            if($i+1 -lt $events.Count){$length=[Math]::Min($length,([long]$events[$i+1].start_ms-[long]$cue.start_ms)*192)}
            if($length -le 0){continue}
            if($start -lt $written){throw 'Non-monotonic sound event timeline'}
            Write-ActorSilence $writer ($start-$written);$writer.Write($data,0,[int]$length);$written=$start+$length
        }
        $total=[Math]::Max($written,$videoMs*192)
        if($total -gt [uint32]::MaxValue-36){throw 'Sound timeline exceeds standard WAV size limit'}
        Write-ActorSilence $writer ($total-$written);$writer.Flush();$stream.Seek(0,[IO.SeekOrigin]::Begin) | Out-Null
        $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'));$writer.Write([uint32]($total+36));$writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
        $writer.Write([uint32]16);$writer.Write([uint16]1);$writer.Write([uint16]2);$writer.Write([uint32]48000);$writer.Write([uint32]192000);$writer.Write([uint16]4);$writer.Write([uint16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes('data'));$writer.Write([uint32]$total);$writer.Flush()
        return $total/192000.0
    }finally{$writer.Dispose()}
}
