# Office Assistant Exporter

Actor Exporter v5 - usable full idle clips, optional debug variants

Place all included scripts and C# files beside FrontierUtopia.dll. Requires
Windows PowerShell 5.1, .NET Framework 4.x and FFmpeg in PATH. FFprobe is used
only by validation scripts. Original ACT/DLL and FFmpeg are not bundled.

One actor:
  .\Build-And-Export-v5.ps1 -Actor .\will.act -Scale 2 -WebP -Output .\WillOutput

Whole folder (one output subfolder per ACT):
  .\Export-Actors.ps1 -ActorsDirectory 'C:\Actors' -Scale 2 -WebP -Output .\OfficeExports

Use action_001_actionIdle_showcase.mp4 (or .webp) as the complete idle clip.
You no longer need to assemble the separate variants yourself.

When one valid continuous graph walk covers the idle repertoire, it is rendered
directly. When one-way branches cannot reconnect, the exporter builds valid
entry-to-ending paths, plays them in a fixed order, and assembles them into ONE
showcase automatically. Each independent section is followed by the engine's
normal ImageShow rest pose for 250 ms before the next action restart. Source
frame and sound timing within each section is unchanged. Segment boundaries
are documented action restarts rather than invented bytecode connections.

Original endings are used when present. If a branch has no transition back to
rest, the normal rest-pose reset can still be visible; the exporter does not
invent interpolated artwork. The showcase is a deterministic presentation of
the repertoire, not the original random probabilities or choreography.

The ordinary action_001_actionIdle.mp4 remains a short seeded sample. Select
the idle_showcase file for the complete assembled repertoire.

Individual variant MP4/WebP files are DEBUG OUTPUT and are disabled by default.
Use -DebugVariants if desired. Variant PNGs and metadata remain as source data
for building the combined clip. Use a new output folder for a clean set when
upgrading; old debug movies in an existing folder are not deleted automatically.

Sound is retained in MP4. Original WAVs are preserved in sounds\ and cue times
are in sound_events.csv. A streamed 48kHz stereo PCM timeline reproduces the
default single sound channel: the next cue replaces the previous one, and gaps
are silent. The final cue may finish after the last animation frame. This avoids
Windows command-length limits and large delayed-sound filter graphs on long
idle assemblies. PCM cache/timeline files are rebuildable intermediates.
Speech synthesis is not generated. WebP has no audio track.

Scaling is artwork-aware. -Scale 2 means 384x288 for the supplied vector actors.
Bitmap, mixed or unknown artwork stays at its original size by default (typically
192x144). Detection inspects the artwork, not names. -ScaleBitmaps overrides this.
Console output and artwork.csv report requested/effective scale and dimensions.

Backgrounds are optional:
  .\Build-And-Export-v5.ps1 -Actor .\will.act -Scale 2 -WebP -BackgroundImage 'C:\Images\desk.jpg'
  .\Build-And-Export-v5.ps1 -Actor .\will.act -WebP -Background '#E8E0D0'

PNG/WebP defaults to transparent, with no image background. Images use a centered
cover crop without distortion. Transparent image areas reveal Background, which
can be Transparent, a .NET color name or quoted #RRGGBB. MP4 uses a dark backing
when source pixels remain transparent. Background settings/content hashes and
effective scale participate in completion signatures.

Finite actions finish in full. Ordinary deterministic loops take an identified
exit on the third pass by default; the last pass may be partial. -Cycles 2 exits
on the second pass. -Cycles 0 retains raw one-cycle/bounded sample behavior.
Random cyclic samples have a time budget (-MaxLoopSeconds, default 10 for one
actor, 3 for the folder runner) and an instruction cap. After sampling, a legal
natural return or identified controlled ending is followed when available.
Finishing can extend the clip beyond the sample budget. No-ending cases remain
bounded and are marked no_ending_found. The full idle tour is not time-capped.

MP4 is H.264/AAC, 60fps, with a half-second final pose hold. frames.csv preserves
the integer-ms source timing; displayed MP4 timing is quantized to its 60fps grid.
WebP repeats its entire clip, including any intro and ending. Loop metadata does
not encode an intro-once/inner-loop playback rule into a standalone media file.

Other options: -NoMP4 -WebP for WebP only; -NoMP4 alone for frames/reports;
-Mode analyze; -NoShowcase (single actor); -Force; -DebugVariants. Relative paths
resolve beside the scripts. Rebuild with -Force after changing ACT/DLL/source.
The older Build-And-Export-v4.ps1 name is retained as a compatible entry point.

Reports: actions.csv, analysis.csv, artwork.csv, frames.csv, sound_events.csv,
opcodes.csv, instructions.csv, playback.csv, coverage/edge CSVs. Assembled idles
have segments.csv, source_variant columns and assembly.txt instead of pretending
their restarts are one instruction path. Unknown action IDs retain their numbers.
UCSH defaultactions.h supplies standard names.

Checks:
  .\Validate-Actors.ps1 -Output .\OfficeExports
  .\Validate-Audio.ps1
  .\Validate-Scaling.ps1 -Actors 'E:\OFFICE\ACTORS' -Render
  .\Validate-Backgrounds.ps1 -Actor .\will.act

Validation checks raw paths, documented exit overrides, frame/sound timings,
idle edge coverage and decoding. Assembled idle sections are checked against
their original variant frames/cues and their declared rest frames. The audio
test checks cue placement, interruption, repeated cues, silence gaps and WAV
decoding. Optional -DebugVariants on Validate-Actors validates debug media too.

Known library limits: these tools read ACT, not Agent ACS. Source metadata and
private FrontierUtopia fields are version-sensitive. The supplied ZVISIBLE
character identifies itself as Invisible and has no visible rendered pixels.
