# Lego Assembly Guide — Handoff

Quick context to pick this up tomorrow. Full design lives at `LEGO_DESIGN.md`.

## What this is

An extension to the **ObjectDetection** sample. New scene: `Samples/2 ObjectDetection/Lego/Scenes/LegoAssembly.unity`. It guides a user through an ordered Lego build, outlining real bricks in passthrough:

- **Red** = current step's target brick OR the upcoming loose brick (the step's `indicatorClass`)
- **Green** (pulse) = correctly placed (single frame on commit) → settles to **Green** persistent only when the build is fully complete
- **Yellow** = a brick/structure from a previously-committed step, while the build is still in progress
- **Grey** = any other recognised Lego that doesn't participate in the recipe at all
- **Hidden** = anything the model does not recognise as Lego

Step 1 commits when the right brick is held still ~1 s. Steps 2+ require the matching detection to stay within `proximityTolerance` (default 0.10 m) of the previous step's last-known position for `_subsequentStepHoldSeconds` (default 1.5 s) before committing. Per-frame commit lock prevents cascading.

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
  - Recipe with per-step indicators:
    - Step 1: `Blue Lego Block`, no indicator
    - Step 2: `Step 2 Structure`, indicator `Green Lego Block`
    - Step 3: `Final Structure`, indicator `Orange Lego Block`
  - Tolerances 0.10 m on all steps, `_subsequentStepHoldSeconds = 1.5`
- Custom YOLOv11 model: `Lego/Resources/blocks.onnx` (raw Ultralytics export) + `blocks.sentis` (NMS-wrapped, 3-output graph matching `LegoDetector`'s `PeekOutput(0..2)` contract)
- Editor utility: `Assets/Editor/YoloOnnxToSentisWrapper.cs` (menu **Tools → Lego → Wrap YOLOv11 ONNX with NMS**) — re-runnable any time the source ONNX is replaced
- Build Settings: `LegoAssembly.unity` enabled, `ObjectDetection.unity` disabled (see `EditorBuildSettings.asset`)
- End-to-end run on Quest 3: build deploys, detection works, recipe advances correctly through all three steps
- **UX polish (verified on-device):**
  - Per-step `indicatorClass` highlights the next loose brick in red while the user is reaching for it (Green block during step 2, Orange block during step 3)
  - Steps 2+ now require the matching detection to stay within `proximityTolerance` of the previous step's position for `_subsequentStepHoldSeconds` (1.5 s) before committing — chime is no longer jumpy and fleeting misdetections don't advance the build
  - Previously-committed step classes render **Yellow** during the build and **Green** when `IsComplete` (so the partial assembly looks "in progress, but earned" mid-build, and the finished build glows green)

### Next session
No outstanding asks. Start a new session with on-device feedback; common knobs are `_subsequentStepHoldSeconds`, `_firstStepHoldSeconds`, `_firstStepStillnessRadius`, and the per-step `proximityTolerance`. All live on `LegoBuildManager` and tune without rebuilding (just redeploy).

## Verification (on Quest 3, PCA needs the device)

Three loose bricks on a table to start.

1. **Start of build (step 1 active):**
   - Blue brick → **Red** outline (current target).
   - Green & Orange bricks → **Grey** (recognised Lego but not the indicator on step 1).
   - Hold the Blue brick still ~1 s → chime + Green pulse.
2. **After step 1 commits (step 2 active):**
   - Blue brick (or any single-blue detection) → **Yellow** (it's now a completed-step class).
   - Green brick → **Red** (indicator for step 2). Orange brick → Grey.
   - Place the Green brick adjacent to Blue → model fires `Step 2 Structure` near the previous position → outline goes Red and **holds** ~1.5 s without committing → chime + Green pulse → settles to **Yellow** (still mid-build).
3. **After step 2 commits (step 3 active):**
   - `Step 2 Structure` (and any leftover blue detections) → **Yellow**.
   - Orange brick → **Red** (indicator for step 3).
   - Place the Orange brick to make the full build → model fires `Final Structure` near the previous position → Red holds ~1.5 s → chime + Green pulse.
4. **Build complete:**
   - `Final Structure` → persistent **Green**. Any leftover `Blue Lego Block` / `Step 2 Structure` detections also Green (they're all recipe classes and the build is done).
5. **Premature-commit regression check.** Wave the partial assembly briefly through-frame or briefly misdetect — the chime should NOT fire unless detection stays in tolerance for the full 1.5 s.
6. **Smoke check logs.** `adb logcat -s Unity` should show `[LegoDetector] Passthrough texture ready: ...` followed by detection events at ~10 Hz.
7. **Regression.** Original `Samples/2 ObjectDetection/ObjectDetection.unity` should still build and run normally (untouched, separate `yolov9sentis.sentis`).

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
- Tuning knobs on `LegoBuildManager` (no rebuild required, just redeploy):
  - `_firstStepHoldSeconds` (default 1.0 s) and `_firstStepStillnessRadius` (default 0.03 m) — first-step debounce
  - `_subsequentStepHoldSeconds` (default 1.5 s) — sustained-detection requirement for steps 2+
  - Per-step `proximityTolerance` — how close the next structure must be to the previous step's last-known position
- Android signing uses `Unity-QuestVisionKit/Keys/quest-debug.keystore` (gitignored), alias `quest-debug`, password `questdebug`. The keystore file does not sync via git. **When setting up a new machine** (e.g. switching between the Windows tower and the MacBook), copy the keystore file from a machine that already has it (AirDrop / scp / USB) into the same path on the new machine. Do **not** regenerate it via the menu unless every machine has lost the file: `keytool` produces a fresh keypair every invocation, so a regenerated keystore is a different key and the Quest will reject the next build with `INSTALL_FAILED_UPDATE_INCOMPATIBLE`. The menu item **Tools → Lego → Set Up Quest Debug Keystore** (`Assets/Editor/SetupQuestKeystore.cs`) is for first-time setup or full disaster recovery — accept that the first Quest install after a regenerated keystore triggers one uninstall+reinstall dialog and a `HEADSET_CAMERA` permission re-grant.
- Unity does **not** persist Android keystore passwords across Editor sessions. Each launch you'll need to re-enter them in **Edit → Project Settings → Player → Android → Publishing Settings** (keystore password `questdebug`, alias `quest-debug`, alias password `questdebug`).
