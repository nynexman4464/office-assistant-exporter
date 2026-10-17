# Office Assistant Exporter

Export classic Microsoft Actor (`.ACT`) animations as MP4 videos, animated WebP files, PNG frames, and timing reports. Includes original sound effects in MP4, readable action names, and automatically assembled full idle showcases.

Built with AI assistance using OpenAI Codex for implementation, documentation, and validation work. See [VALIDATION.txt](VALIDATION.txt) for recorded export checks.

### Example: Will's idle showcase

![Will performing his idle animation repertoire](docs/media/will-idle.webp)

A transparent animated WebP exported at 2× scale. The showcase includes the reachable idle branches, rather than just a short random sample. This example contains original character artwork; the repository's code license does not apply to it.

## Requirements

- **Windows PowerShell 5.1** and **.NET Framework 4.x**. The scripts use the Framework compiler; a .NET SDK is not required.
- **[Universal Character Scripting Helper (UCSH)](https://tmafe.com/ucsh)** for `FrontierUtopia.dll`.
- **[FFmpeg](https://ffmpeg.org/download.html)** available on your `PATH`, with H.264, AAC, and animated WebP encoding support. Validation also uses `ffprobe`.
- Your own **`.ACT` character files**. Microsoft Agent `.ACS` files are not supported.

UCSH, FFmpeg, and character files are not bundled.

## Quick start

1. Clone or download this repository and extract it to a folder you can write to. Keep all `.cs` and `.ps1` files together.
2. [Download and install UCSH](https://tmafe.com/ucsh_latest.html). Copy `FrontierUtopia.dll` from its installation folder (typically `C:\Program Files (x86)\UCSH`) into the exporter folder.
3. Install FFmpeg and add its executable folder to `PATH`. Open a new **Windows PowerShell** window and check:

   ```powershell
   ffmpeg -version
   ffprobe -version
   ```

4. Open PowerShell in the exporter folder. Run this command, replacing the actor path with your own:

   ```powershell
   .\Build-And-Export-v5.ps1 -Actor 'C:\Actors\will.act' -Scale 2 -WebP -Output .\WillOutput
   ```

The script compiles the exporter automatically, renders the animations, and creates MP4 and WebP files in `WillOutput`. The first export can take a while, especially for long idle showcases. Relative paths resolve beside the scripts.

If Windows blocks a downloaded script, review the files, then use `Unblock-File` on the scripts you downloaded. If your PowerShell execution policy blocks scripts, use an execution policy permitted by your Windows environment.

## Export a whole folder

```powershell
.\Export-Actors.ps1 -ActorsDirectory 'C:\Actors' -Scale 2 -WebP -Output .\OfficeExports
```

Each `.ACT` file directly inside that folder gets its own output subfolder. Subfolders are not searched. `batch.csv` records each actor's result, dimensions, and any error. If one actor fails, the runner continues with the remaining actors and reports the failure at the end.

## Which files should I use?

| Output | Purpose |
| --- | --- |
| `action_001_actionIdle_showcase.mp4` | Full idle repertoire, automatically assembled into one usable video. |
| `action_001_actionIdle.mp4` | Short, repeatable sample of the original random idle behavior. |
| Other `action_###_actionName.mp4` files | Individual actions with sound effects. |
| Matching `.webp` files | Animated images with transparency by default; no audio. Requires `-WebP`. |
| Action subfolders | Source PNG frames, timing data, and playback reports. |
| `sounds/` | Original extracted WAV sound effects. |

Standard action names come from UCSH's `defaultactions.h`; unknown action IDs keep their numeric identifier.

## Common options

These options work with both the single-actor and folder scripts unless noted.

| Option | Behavior |
| --- | --- |
| `-Scale 2` | Render vector artwork at twice its original dimensions. Accepts 1–12. Default: 4 for a single actor, 2 for a folder. |
| `-ScaleBitmaps` | Apply the requested scale to bitmap, mixed, and unknown artwork too. Otherwise they stay at native size. |
| `-WebP` | Create animated WebP files alongside MP4. |
| `-NoMP4 -WebP` | Create WebP files without MP4. |
| `-NoMP4` | Export frames and reports without encoding videos. |
| `-Background '#E8E0D0'` | Fill transparent areas with a color. Accepts .NET color names, quoted `#RRGGBB`, or `Transparent`. |
| `-BackgroundImage 'C:\Images\desk.jpg'` | Add a background image, centered and cropped to fill the canvas without stretching. |
| `-Cycles 3` | Take an identified ending on the third loop pass when available. Accepts 0–10; default 3. |
| `-MaxLoopSeconds 10` | Set the sample budget for random cyclic actions. Default: 10 for a single actor, 3 for a folder. |
| `-Mode analyze` | Write analysis reports without rendering frames or encoding media. |
| `-Force` | Rebuild existing output. Use after changing an ACT file, DLL, or exporter source. |
| `-DebugVariants` | Also encode the separate idle variant videos used to assemble a showcase. |
| `-NoShowcase` | Skip the full idle showcase. Single-actor script only. |

### Scaling

Artwork detection examines the resources inside each actor. Pure vector artwork uses the requested scale; bitmap, mixed, or unknown artwork stays at its original size unless you use `-ScaleBitmaps`. For a 192×144 vector actor, `-Scale 2` produces 384×288 media.

The console and `artwork.csv` report the detected type and effective dimensions.

### Backgrounds and transparency

PNG frames and WebP files default to transparent backgrounds. MP4 uses a dark backing wherever pixels remain transparent.

```powershell
# Image background
.\Build-And-Export-v5.ps1 -Actor 'C:\Actors\will.act' -Scale 2 -WebP -BackgroundImage 'C:\Images\desk.jpg'

# Solid color background
.\Build-And-Export-v5.ps1 -Actor 'C:\Actors\will.act' -Scale 2 -WebP -Background '#E8E0D0'
```

You can combine a background image with a color: transparent parts of the image reveal that color. Background settings, image content hashes, and effective scale participate in output completion signatures.

## How loops and idle showcases work

**Finite actions** play through to completion.

**Deterministic loops** take an identified exit on the third pass by default. The final pass may be partial. `-Cycles 2` selects the second pass; `-Cycles 0` keeps the raw intro-plus-one-cycle behavior for deterministic loops and bounded samples for random loops.

**Random cyclic actions** use a fixed seed, a time budget, and an instruction safety limit. After sampling, the exporter follows a legal natural return or an identified controlled ending when available. This ending can extend the clip beyond the sample budget. Cases without an ending stay bounded and are marked `no_ending_found`.

**Full idle showcases** cover the reachable idle repertoire in a fixed order and are not time-capped. When the branches reconnect, the exporter creates one continuous walk. When they cannot reconnect, it assembles separate valid action paths into one showcase, placing the engine's normal rest pose between sections for 250 ms. Frame and sound timing within each section is preserved.

Showcases present the repertoire in a repeatable order rather than reproducing the original random probabilities. Rest-pose resets can be visible when the source has no transition back to rest. Separate variant PNGs and metadata remain as assembly inputs; variant movies are only created with `-DebugVariants`.

### Why a short idle export can miss animations

An idle action is a small program with jumps and random choices, rather than a single list of frames. One choice might return to the resting pose, while another starts a longer activity, such as Will's writing animation. A short random recording can revisit the same branch repeatedly and finish before reaching the others.

The diagram below is a simplified illustration, not a literal map of Will's instructions:

```mermaid
flowchart TD
    Entry[Start idle action] --> Choice{Random branch}
    Choice --> A[Short idle movement]
    Choice --> B[Longer activity, such as writing]
    Choice --> C[Another idle movement]
    A --> Choice
    B --> Choice
    C --> Choice
```

The showcase replaces random selection with a fixed plan that covers every reachable instruction transition. It follows actual connecting paths between branches; simply ignoring a jump could skip artwork or leave an activity unfinished. Coverage describes the graph's transitions, not every possible combination or number of repetitions.

Some actors have one-way branches that cannot reconnect. In that case, the exporter makes complete sections starting from the action entry, then joins them into the final showcase:

```mermaid
flowchart LR
    A[Valid idle section A] --> R1[Rest pose: 250 ms]
    R1 --> B[Restart action: section B]
    B --> R2[Rest pose: 250 ms]
    R2 --> C[Restart action: section C]
```

These are explicit action restarts, not invented transitions inside the original program. You receive one `action_001_actionIdle_showcase` file; the underlying variants are assembly inputs, and separate variant movies are optional debug output.

## Audio and playback

- MP4 uses **H.264/AAC at 60 fps**, with a half-second final pose hold.
- Original sound effects are preserved as WAV files and scheduled on one sound channel: each new cue replaces the previous cue, and gaps are silent. The final cue may finish after the final animation frame.
- Speech synthesis is not generated. WebP has no audio track.
- `frames.csv` preserves source timing in integer milliseconds. MP4 timing is rounded to its 60 fps grid.
- WebP repeats the entire clip, including any intro and ending. It does not play an intro once and then repeat only an inner loop.

## Reports and validation

`actions.csv` indexes the exported actions. `analysis.csv` describes their instruction graphs and loop classifications. Each action also has frame, sound, instruction, opcode, and playback reports. Showcase coverage reports describe visited instructions and edges; assembled showcases include `segments.csv`, source-variant columns, and `assembly.txt` documenting the restarts.

To validate a folder export:

```powershell
.\Validate-Actors.ps1 -Output .\OfficeExports
```

Additional checks:

```powershell
.\Validate-Audio.ps1
.\Validate-Scaling.ps1 -Actors 'C:\Actors' -Render
.\Validate-Backgrounds.ps1 -Actor 'C:\Actors\will.act'
```

Validation checks instruction transitions, documented exit overrides, frame and sound timing, idle coverage, and media decoding. Assembled showcase sections are compared with their source variants and declared rest frames. Add `-DebugVariants` to `Validate-Actors.ps1` to require debug media checks too.

## Troubleshooting and limitations

- **Missing DLL:** copy `FrontierUtopia.dll` beside the scripts, rather than into the output folder.
- **FFmpeg not found:** ensure its executable folder is on `PATH`, then open a new PowerShell window.
- **Old results after changing inputs:** rerun with `-Force`. ACT, DLL, and source changes are not automatically detected by the completion cache.
- **Extra variant movies after upgrading:** use a new output folder for a clean export. Old files are not deleted automatically.
- **Unexpected bitmap dimensions:** native-size preservation is intentional. Use `-ScaleBitmaps` to override it.
- The exporter reads private FrontierUtopia fields, so compatibility can depend on the installed library version.
- The `ZVISIBLE` character identifies itself as Invisible and has no visible rendered pixels.
- `Build-And-Export-v4.ps1` remains as a compatible entry point; new users should use `Build-And-Export-v5.ps1`.

## Credits and license

Credit to **[Universal Character Scripting Helper (UCSH)](https://tmafe.com/ucsh)** and **Konnor88 / TMAFE**. The exporter uses the Frontier Actor library distributed with UCSH (`FrontierUtopia.dll`) to load and render ACT characters. Standard action names come from UCSH's `defaultactions.h`.

Get UCSH from its [official download page](https://tmafe.com/ucsh_latest.html).

The exporter code is covered by this repository's [BSD 3-Clause license](LICENSE). That license does not cover UCSH, Frontier Actor, FFmpeg, or original character artwork and sounds; those remain subject to their respective terms. This repository does not distribute the UCSH DLL or character assets.
