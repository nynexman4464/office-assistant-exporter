param([string]$Actors='E:\OFFICE\ACTORS',[switch]$Render)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    $root=Join-Path $PSScriptRoot ('ScaleValidation-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'));New-Item -ItemType Directory -Path $root -Force | Out-Null
    # Compile using the same Windows PowerShell/.NET Framework path as users.
    & .\Build-And-Export-v4.ps1 -Actor (Join-Path $Actors 'WILL.ACT') -Mode analyze -Scale 2 -Output (Join-Path $root 'WILL')
    foreach($file in Get-ChildItem -LiteralPath $Actors -File -Filter *.ACT){
        $out=Join-Path $root $file.BaseName
        & .\WillActorExporter.exe $file.FullName $out 2 analyze 3 false none 3
        if($LASTEXITCODE -ne 0){throw 'Analysis failed'}
        $r=Import-Csv -LiteralPath (Join-Path $out 'artwork.csv')
        $isBitmap=$file.BaseName -in 'GENIUS','MNATURE'
        if($r.kind -ne $(if($isBitmap){'bitmap'}else{'vector'}) -or $r.effective_scale -ne $(if($isBitmap){'1'}else{'2'})){throw "Unexpected scale policy: $($file.Name)"}
    }
    & .\Build-And-Export-v4.ps1 -Actor (Join-Path $Actors 'GENIUS.ACT') -Mode analyze -Scale 2 -ScaleBitmaps -Output (Join-Path $root 'Override')
    $r=Import-Csv -LiteralPath (Join-Path $root 'Override\artwork.csv')
    if($r.effective_scale -ne '2' -or $r.scale_bitmaps_override -ne 'yes'){throw 'Bitmap override failed'}
    if($Render){
        & .\Build-And-Export-v4.ps1 -Actor (Join-Path $Actors 'MNATURE.ACT') -Scale 2 -MaxLoopSeconds 3 -WebP -Output (Join-Path $root 'NativeBitmap')
        & .\Build-And-Export-v4.ps1 -Actor (Join-Path $Actors 'CLIPPIT.ACT') -Scale 2 -MaxLoopSeconds 1 -NoMP4 -WebP -Output (Join-Path $root 'ScaledVector')
        Add-Type -AssemblyName System.Drawing
        foreach($test in @(@('NativeBitmap',192,144),@('ScaledVector',384,288))){
            $out=Join-Path $root $test[0]
            foreach($png in Get-ChildItem -LiteralPath $out -Recurse -Filter frame_0000.png){
                $im=[Drawing.Image]::FromFile($png.FullName);try{if($im.Width -ne $test[1] -or $im.Height -ne $test[2]){throw 'PNG dimensions incorrect'}}finally{$im.Dispose()}
            }
            $files=@(Get-ChildItem -LiteralPath $out -File | Where-Object {$_.Extension -in '.mp4','.webp'})
            foreach($f in $files){
                $p=(& ffprobe -v error -show_streams -of json $f.FullName) -join "`n" | ConvertFrom-Json
                if($LASTEXITCODE -ne 0){throw 'Cannot inspect media'}
                $v=$p.streams | Where-Object codec_type -eq 'video'
                if($v.width -ne $test[1] -or $v.height -ne $test[2]){throw 'Media dimensions incorrect'}
                & ffmpeg -v error -i $f.FullName -fps_mode passthrough -enc_time_base 1/1000 -f null -
                if($LASTEXITCODE -ne 0){throw 'Media decode failed'}
            }
        }
    }
    & .\WillActorExporter.exe --self-test
    if($LASTEXITCODE -ne 0){throw 'Logic tests failed'}
    Write-Host 'PASS: all nine artwork classifications; seven vector actors honor 2x; Genius/Mother Nature stay 1x; explicit override works. Optional render checks validate native bitmap and scaled vector PNG/WebP/MP4 dimensions and decoding.'
} finally {Pop-Location}
