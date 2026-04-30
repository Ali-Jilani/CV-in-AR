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
- `Resources/LegoClasses.txt` — 5 model classes (Blue / Green / Orange / Step 2 Structure / Final Structure)
- `LegoAssembly.unity` scene wired:
  - Detection GameObject: `LegoDetector` (`sentisModel` → `Lego/Resources/blocks.sentis`) + `LegoBrickHighlightRenderer`
  - `BuildManager` GameObject: `LegoBuildManager` + `AudioSource` (`ReceiveResponse.wav`, `PlayOnAwake = false`)
  - Recipe: `Blue Lego Block → Step 2 Structure → Final Structure`, all 0.10 m tolerance
- Custom YOLOv11 model: `Lego/Resources/blocks.onnx` (raw Ultralytics export) + `blocks.sentis` (NMS-wrapped, 3-output graph matching `LegoDetector`'s `PeekOutput(0..2)` contract)
- Editor utility: `Assets/Editor/YoloOnnxToSentisWrapper.cs` (menu **Tools → Lego → Wrap YOLOv11 ONNX with NMS**) — re-runnable any time the source ONNX is replaced

### Remaining

1. **Add to Build Settings.** File → Build Settings → add `LegoAssembly.unity`, uncheck `ObjectDetection.unity` if you want this to be the default scene on device.

## Verification (on Quest 3, PCA needs the device)

1. Three loose bricks on a table:
   - Blue brick → **red** outline
   - Green & Orange bricks → **yellow** outlines (recognised Lego, not the current target)
2. Hold the Blue brick still for ~1 s → chime + green pulse → settles to grey. Green/Orange remain yellow.
3. Place the Green brick adjacent to the Blue (within 10 cm of Blue's last-known position) — the model now sees the partial assembly as `Step 2 Structure` → red flicker → green pulse → chime → grey.
4. Add the Orange brick to make the full build → model fires `Final Structure` near the previous step's position → green pulse → chime → idle.
5. Smoke check: `adb logcat -s Unity` should show `[LegoDetector] Passthrough texture ready: ...` followed by detection events at ~10 Hz.
6. Regression: original `Samples/2 ObjectDetection/ObjectDetection.unity` should still build and run normally (untouched, separate `yolov9sentis.sentis`).

If detections are noisy, re-run **Tools → Lego → Wrap YOLOv11 ONNX with NMS** after editing `IouThreshold` / `ScoreThreshold` constants in `YoloOnnxToSentisWrapper.cs` (defaults 0.45 / 0.25).

## Key file paths

- Scripts: `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Lego/Scripts/`
- Scene: `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Lego/Scenes/LegoAssembly.unity`
- Prefab: `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Lego/Prefabs/LegoHighlight.prefab`
- Materials: `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Lego/Materials/`
- Class labels: `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Lego/Resources/LegoClasses.txt`
- Detection model: `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Lego/Resources/blocks.sentis` (NMS-wrapped) + `blocks.onnx` (raw)
- NMS wrapper utility: `Unity-QuestVisionKit/Assets/Editor/YoloOnnxToSentisWrapper.cs`
- Full plan: `LEGO_DESIGN.md` (repo root)

## Known caveats

- `LegoHighlight.prefab`, the four `.mat` files, and the four script `.meta` files were authored as YAML by Claude (not via Unity Editor). They imported cleanly, but if Unity ever flags a missing-reference on these, the GUIDs to check are `1eda000000000000000000000000a001` through `…a00a` (see the `.meta` files).
- The first-step debounce (`_firstStepHoldSeconds = 1.0`, `_firstStepStillnessRadius = 0.03 m`) is in `LegoBuildManager` — bump those if the chime keeps misfiring.
