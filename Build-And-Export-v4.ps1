param(
    [string]$Actor='.\will.act',
    [ValidateRange(1,12)][int]$Scale=4,
    [string]$Output='.\WillOutput-Endings',
    [ValidateSet('analyze','export')][string]$Mode='export',
    [ValidateRange(1,600)][int]$MaxLoopSeconds=10,
    [switch]$Force,
    [switch]$WebP,
    [switch]$NoShowcase,
    [switch]$NoMP4,
    [ValidateRange(0,10)][int]$Cycles=3,
    [ValidateNotNullOrEmpty()][string]$Background='Transparent',
    [string]$BackgroundImage='',
    [switch]$ScaleBitmaps,
    [switch]$DebugVariants
)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    if(-not (Test-Path -LiteralPath '.\FrontierUtopia.dll')){throw 'Copy FrontierUtopia.dll beside this script.'}
    if(-not (Test-Path -LiteralPath $Actor)){throw "ACT not found: $Actor"}
    $csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
    & $csc /nologo /target:exe /out:WillActorExporter.exe /reference:FrontierUtopia.dll /reference:System.Drawing.dll WillActorExporter.cs AudioShowcase.cs ClipPlanner.cs IdleVariants.cs Backdrop.cs ArtworkScale.cs IdleAssembly.cs
    if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
    $showcaseMode=if($NoShowcase){'none'}else{'showcase'}
    $imageArg=if([string]::IsNullOrEmpty($BackgroundImage)){'-'}else{$BackgroundImage}
    & .\WillActorExporter.exe $Actor $Output $Scale $Mode $MaxLoopSeconds $Force.IsPresent.ToString().ToLowerInvariant() $showcaseMode $Cycles $Background $imageArg $ScaleBitmaps.IsPresent.ToString().ToLowerInvariant()
    if($LASTEXITCODE -ne 0){throw 'Export failed'}
    if($Mode -eq 'export' -and (-not $NoMP4 -or $WebP)){& .\Convert-MediaLinear.ps1 -Output $Output -Force:$Force -WebP:$WebP -NoMP4:$NoMP4 -DebugVariants:$DebugVariants}
} finally {Pop-Location}
