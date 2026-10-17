param([string]$Actor='.\will.act')
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    Add-Type -AssemblyName System.Drawing
    $root=Join-Path $PSScriptRoot ('BackgroundValidation-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'));New-Item -ItemType Directory -Path $root -Force | Out-Null
    $image=Join-Path $root 'image with spaces.png';$out=Join-Path $root 'Exports'
    function MakeFixture([bool]$changed) {
        $b=New-Object Drawing.Bitmap 400,100;$g=[Drawing.Graphics]::FromImage($b)
        $g.FillRectangle([Drawing.Brushes]::Green,0,0,400,100)
        $brush=if($changed){[Drawing.Brushes]::Yellow}else{[Drawing.Brushes]::Red}
        $g.FillRectangle($brush,100,0,100,100);$g.FillRectangle([Drawing.Brushes]::Blue,200,0,100,100)
        $b.Save($image);$g.Dispose();$b.Dispose()
    }
    function Pixel([string]$path,[int]$x=0) {$b=[Drawing.Bitmap]::FromFile($path);try{return $b.GetPixel($x,0)}finally{$b.Dispose()}}
    function Render([string]$color='Transparent',[string]$img='') {
        & .\Build-And-Export.ps1 -Actor $Actor -Scale 1 -MaxLoopSeconds 1 -Output $out -Background $color -BackgroundImage $img -NoMP4 -WebP
    }
    Render
    $png=Join-Path $out '002_greetingShow\frame_0000.png'
    if((Pixel $png).A -ne 0){throw 'Default PNG must retain transparency'}
    $decoded=Join-Path $root 'webp-first.png'
    & ffmpeg -v error -y -i (Join-Path $out '002_greetingShow.webp') -frames:v 1 $decoded
    if($LASTEXITCODE -ne 0 -or (Pixel $decoded).A -ne 0){throw 'Default WebP must retain transparency'}
    if(@(Get-ChildItem -LiteralPath $out -Filter *.mp4).Count){throw '-NoMP4 unexpectedly created MP4'}
    $timeline=Join-Path $out '022_writingNotingSomething\frames.csv';$audio=Join-Path $out '022_writingNotingSomething\sound_events.csv'
    $framesHash=(Get-FileHash -LiteralPath $timeline).Hash;$audioHash=(Get-FileHash -LiteralPath $audio).Hash
    Render '#225588'
    if((Pixel $png).ToArgb().ToString('X8') -ne 'FF225588'){throw 'Solid color mismatch'}
    MakeFixture $false;Render 'Transparent' $image
    if((Pixel $png).ToArgb().ToString('X8') -ne 'FFFF0000' -or (Pixel $png 191).ToArgb().ToString('X8') -ne 'FF0000FF'){throw 'Image cover/crop/opacity mismatch'}
    $marker=Join-Path $out '002_greetingShow\export.complete';$signature=Get-Content -LiteralPath $marker -Raw
    MakeFixture $true;Render 'Transparent' $image
    if((Pixel $png).ToArgb().ToString('X8') -ne 'FFFFFF00' -or (Get-Content -LiteralPath $marker -Raw) -eq $signature){throw 'Edited image did not invalidate cache'}
    $modified=(Get-Item -LiteralPath $png).LastWriteTimeUtc;Render 'Transparent' $image
    if((Get-Item -LiteralPath $png).LastWriteTimeUtc -ne $modified){throw 'Unchanged background did not reuse cache'}
    if((Get-FileHash -LiteralPath $timeline).Hash -ne $framesHash -or (Get-FileHash -LiteralPath $audio).Hash -ne $audioHash){throw 'Background altered animation or sound timing'}
    & .\Build-And-Export.ps1 -Actor $Actor -Scale 1 -MaxLoopSeconds 1 -Output $out -BackgroundImage $image
    if(@(Get-ChildItem -LiteralPath $out -Filter *.mp4).Count -ne 36){throw 'Expected 36 MP4s'}
    & ffmpeg -v error -y -i (Join-Path $out '002_greetingShow.mp4') -frames:v 1 $decoded
    $corner=Pixel $decoded
    if($LASTEXITCODE -ne 0 -or $corner.R -lt 220 -or $corner.G -lt 220 -or $corner.B -gt 40){throw 'MP4 did not inherit image background'}
    & .\WillActorExporter.exe --self-test
    if($LASTEXITCODE -ne 0){throw 'Animation logic tests failed'}
    Write-Host 'PASS: default PNG/WebP alpha, solid color, image cover/crop/opaque edges, spaced image path, edited-image cache invalidation, unchanged-image cache reuse, frame/sound timing unchanged, WebP-only mode and MP4 background.'
} finally {Pop-Location}
