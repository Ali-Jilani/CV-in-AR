# Lego Assembly Guide — Handoff

Quick context to pick this up tomorrow. Full design lives at `LEGO_DESIGN.md`.

## What this is

An extension to the **ObjectDetection** sample. New scene: `Samples/2 ObjectDetection/Lego/Scenes/LegoAssembly.unity`. It guides a user through an ordered Lego build, outlining real bricks in passthrough:

- **Red** = brick type required for the current step
- **Yellow** = recognised Lego, but not what's needed now
- **Green** (pulse) = correctly placed
- **Grey** = previously placed

Step 1 commits when the right brick is held still ~1 s. Steps 2+ commit when the right brick is detected within `proximityTolerance` (default 0.10 m) of the previous step's last-known position. Per-frame commit lock prevents cascading.

The original `ObjectDetection.unity` is untouched.

## Status

### Done
- Scripts compile cleanly (`LegoDetector`, `LegoBrickHighlightRenderer`, `LegoBuildManager`, `LegoBrickHighlight`)
- 4 state materials (Red / Yellow / Green / Grey) under `Lego/Materials/`
- `LegoHighlight.prefab` (quad + script + materials wired)
- `Resources/LegoClasses.txt` — placeholder class names
- `LegoAssembly.unity` scene duplicated and wired up via MCP:
  - Detection GameObject: `LegoDetector` + `LegoBrickHighlightRenderer` (with prefab + BuildManager refs set)
  - `BuildManager` GameObject: `LegoBuildManager` + `AudioSource` (`PlayOnAwake = false`)
  - Recipe pre-populated with placeholder steps: `red_2x4 → blue_2x2 → red_2x4`, all 0.10 m tolerance
  - `_successChime` references the sibling AudioSource

### Remaining

1. **(REQUIRED) Wire the audio clip.** MCP couldn't persist this — has to be done by hand.
   - Open `LegoAssembly.unity`
   - Select `BuildManager` in the Hierarchy
   - Drag `Assets/Samples/5 ImageLLM/Audio/ReceiveResponse.wav` onto the `AudioSource → AudioClip` slot

2. **(REQUIRED for real detection) Replace the placeholder model.**
   - `LegoDetector → Sentis Model` currently points at the COCO `yolov9sentis.sentis` — it won't recognise Lego bricks
   - Drop your custom-trained Lego `.sentis` into `Lego/Resources/`
   - Drag onto `LegoDetector → Sentis Model`
   - Update `Lego/Resources/LegoClasses.txt` so each line matches a class index from your model
   - Update the recipe `brickClass` strings in `BuildManager → LegoBuildManager → _recipe` to match

3. **Add to Build Settings.** File → Build Settings → add `LegoAssembly.unity`, uncheck `ObjectDetection.unity` if you want this to be the default scene on device.

## Verification (on Quest 3, PCA needs the device)

1. Three loose bricks on a table:
   - Step-1 brick → **red**
   - Step-2 and step-3 bricks → **yellow**
   - Non-Lego objects → no outline
2. Hold step-1 brick still for ~1 s → chime + green pulse → settles to grey. Step-2 brick now turns red.
3. Place step-2's brick > 10 cm from step-1 → stays **red** (no chime). Move within 10 cm → green pulse, step-3 turns red.
4. After final step → chime, then idle.
5. Smoke check: `adb logcat -s Unity` should show `[LegoDetector] Passthrough texture ready: ...` followed by detection events at ~10 Hz.
6. Regression: original `ObjectDetection.unity` should still build and run normally.

## Key file paths

- Scripts: `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Lego/Scripts/`
- Scene: `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Lego/Scenes/LegoAssembly.unity`
- Prefab: `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Lego/Prefabs/LegoHighlight.prefab`
- Materials: `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Lego/Materials/`
- Class labels: `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Lego/Resources/LegoClasses.txt`
- Full plan: `LEGO_DESIGN.md` (repo root)

## Known caveats

- `LegoHighlight.prefab`, the four `.mat` files, and the four script `.meta` files were authored as YAML by Claude (not via Unity Editor). They imported cleanly, but if Unity ever flags a missing-reference on these, the GUIDs to check are `1eda000000000000000000000000a001` through `…a00a` (see the `.meta` files).
- The first-step debounce (`_firstStepHoldSeconds = 1.0`, `_firstStepStillnessRadius = 0.03 m`) is in `LegoBuildManager` — bump those if the chime keeps misfiring.
