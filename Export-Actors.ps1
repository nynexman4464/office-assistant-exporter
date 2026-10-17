param(
    [Parameter(Mandatory=$true)][string]$ActorsDirectory,
    [string]$Output='.\ActorExports',
    [ValidateRange(1,12)][int]$Scale=2,
    [ValidateRange(1,600)][int]$MaxLoopSeconds=3,
    [ValidateRange(0,10)][int]$Cycles=3,
    [ValidateSet('analyze','export')][string]$Mode='export',
    [string]$Background='Transparent',[string]$BackgroundImage='',
    [switch]$WebP,[switch]$NoMP4,[switch]$Force,[switch]$ScaleBitmaps,[switch]$DebugVariants
)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    $inputRoot=(Resolve-Path -LiteralPath $ActorsDirectory).Path
    $outputRoot=if([IO.Path]::IsPathRooted($Output)){[IO.Path]::GetFullPath($Output)}else{[IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Output))}
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    $files=@(Get-ChildItem -LiteralPath $inputRoot -File -Filter *.act)
    if(-not $files.Count){throw 'No ACT files found in ActorsDirectory'}
    $results=New-Object 'Collections.Generic.List[object]'
    foreach($file in $files){
        $target=Join-Path $outputRoot $file.BaseName
        Write-Host ('Exporting '+$file.Name)
        try {
            & .\Build-And-Export.ps1 -Actor $file.FullName -Output $target -Scale $Scale -MaxLoopSeconds $MaxLoopSeconds -Cycles $Cycles -Mode $Mode -Background $Background -BackgroundImage $BackgroundImage -WebP:$WebP -NoMP4:$NoMP4 -Force:$Force -ScaleBitmaps:$ScaleBitmaps -DebugVariants:$DebugVariants
            $art=Import-Csv -LiteralPath (Join-Path $target 'artwork.csv')
            $mp4=@(Get-ChildItem -LiteralPath $target -Filter *.mp4 | Where-Object {$DebugVariants -or $_.Name -notmatch '_variant_\d+\.mp4$'}).Count
            $webps=@(Get-ChildItem -LiteralPath $target -Filter *.webp | Where-Object {$DebugVariants -or $_.Name -notmatch '_variant_\d+\.webp$'}).Count
            $results.Add([pscustomobject]@{actor=$file.BaseName;status=if($Mode -eq 'analyze'){'ANALYZED'}else{'EXPORTED'};artwork=$art.kind;scale=$art.effective_scale;width=$art.width;height=$art.height;mp4s=$mp4;webps=$webps;error=''})
        }catch{
            $results.Add([pscustomobject]@{actor=$file.BaseName;status='FAILED';artwork='';scale='';width='';height='';mp4s=0;webps=0;error=$_.ToString()})
            Write-Warning ('Actor failed: '+$file.Name+' - '+$_.ToString())
        }
        $results | Export-Csv -LiteralPath (Join-Path $outputRoot 'batch.csv') -NoTypeInformation
    }
    if(@($results | Where-Object status -eq 'FAILED').Count){throw 'Some actors failed; see batch.csv'}
}finally{Pop-Location}
