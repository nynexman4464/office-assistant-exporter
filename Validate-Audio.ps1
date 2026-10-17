$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'MediaAudio.ps1')
$root=Join-Path $PSScriptRoot ('AudioValidation-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'));New-Item -ItemType Directory -Path $root -Force | Out-Null
$a=New-Object byte[] (192*200);for($i=0;$i -lt $a.Length;$i++){$a[$i]=17}
$b=New-Object byte[] (192*50);for($i=0;$i -lt $b.Length;$i++){$b[$i]=34}
$pcm=@{};$pcm[(Join-Path $root 'a.wav')]=$a;$pcm[(Join-Path $root 'b.wav')]=$b
$events=@([pscustomobject]@{file='a.wav';start_ms=0},[pscustomobject]@{file='b.wav';start_ms=100},[pscustomobject]@{file='b.wav';start_ms=150})
$file=Join-Path $root 'timeline.wav';$duration=Write-ActorAudio $events $root $pcm 300 $file;$bytes=[IO.File]::ReadAllBytes($file)
if($duration -ne 0.3 -or $bytes.Length -ne 44+192*300 -or [Text.Encoding]::ASCII.GetString($bytes,0,4) -ne 'RIFF'){throw 'Incorrect PCM timeline length/header'}
if($bytes[44] -ne 17 -or $bytes[44+192*99] -ne 17 -or $bytes[44+192*100] -ne 34 -or $bytes[44+192*199] -ne 34 -or $bytes[44+192*200] -ne 0){throw 'Cue replacement/gap alignment incorrect'}
& ffmpeg -v error -i $file -f null -
if($LASTEXITCODE -ne 0){throw 'PCM timeline does not decode'}
Write-Host 'PASS: streaming PCM cue placement, interruption, repeated cues, silence gaps, duration/header and decoding.'
