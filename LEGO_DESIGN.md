# Lego Assembly Guide — Plan

## Context

The user wants to extend the existing **ObjectDetection** sample (`Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/`) into a guided Lego-assembly experience on Quest 3. The current sample runs YOLOv9 (Sentis) trained on COCO-80 and renders generic markers over any detected object — it has no concept of "task," "step," or "correctness."

The new feature guides a user through an ordered sequence of brick placements. As the user works, each detected brick is outlined in passthrough with one of four colors:

- **Red** — the brick type required for the *current* step
- **Yellow** — a recognized Lego brick that isn't needed at this step (distractor)
- **Green** — pulses for ~1 s when a brick is correctly placed (right type, within spatial tolerance of the previous step's brick)
- **Grey** — a brick already correctly placed in a prior step

Brainstorming decisions (all locked in by the user):

| Topic | Decision |
|---|---|
| Detection model | Custom-trained YOLO `.sentis` (user-supplied; out of scope for this plan). Code must be label-list-driven, not enum-driven. |
| Visual guide form | In-world outlines on real bricks only — **no** ghost preview, **no** floating panel. |
| Verification | **Spatial proximity**: step 1 = presence; step *N* ≥ 2 = required type detected within `proximityTolerance` (default 10 cm) of step *N−1*'s last-known world position. |
| Recipe authoring | Hardcoded `List<BuildStep>` field on the manager script (no ScriptableObject, no JSON). |
| Distractors | Recognized non-target bricks → **yellow** outline. |
| Completion | Chime + brief flash, then idle. No auto-reset, no panel. Restart by re-running the scene. |

The existing `ObjectDetection.unity` scene must remain untouched — this work lands as a parallel scene + parallel scripts under a new subfolder so the original sample keeps demonstrating generic COCO detection.

## Approach

Duplicate the scene, copy and refactor the detector to a label-list pattern, replace the renderer with a state-aware highlight, and add a build-manager state machine that drives color assignments against a hardcoded recipe.

### File layout (all new)

```
Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Lego/
  Scripts/
    LegoDetector.cs                  # copy of ObjectDetector.cs, label list instead of enum
    LegoBuildManager.cs              # state machine, recipe, proximity check
    LegoBrickHighlightRenderer.cs    # replaces ObjectRenderer; sets state per detection
    LegoBrickHighlight.cs            # MarkerController-style component with SetState()
  Resources/
    lego_yolo.sentis                 # placeholder; user drops real model here
    LegoClasses.txt                  # one class name per line, matches model
  Prefabs/
    LegoHighlight.prefab             # translucent quad; renderer.material instanced per spawn
  Materials/
    LegoHighlight_Red.mat
    LegoHighlight_Yellow.mat
    LegoHighlight_Green.mat
    LegoHighlight_Grey.mat
  Scenes/
    LegoAssembly.unity               # duplicated from ObjectDetection.unity
```

### Architecture

**1. `LegoDetector.cs`** — fork of `ObjectDetector.cs`. Two changes only:

- Replace `YOLOv9Labels` enum lookup with a `string[]` loaded from `Resources/LegoClasses.txt` at `Awake()` (one class per line, matches output index).
- Expose a public `event Action<List<LegoDetection>> OnDetections` where `LegoDetection` carries `{ string className, Vector2 viewportCenter, Rect viewportBox, float confidence }`. The consumer (renderer + manager) subscribes; this decouples detection from rendering.

The existing inference loop (Sentis async schedule, 20-layer chunking, ~10 Hz cadence at `inferenceInterval`) is preserved verbatim. Lines to mirror: `ObjectDetector.cs:1–230`.

**2. `LegoBuildManager.cs`** — the only stateful component. Owns:

```csharp
[Serializable] public struct BuildStep {
    public string brickClass;          // must match a name in LegoClasses.txt
    public float proximityTolerance;   // metres, default 0.10
}

[SerializeField] private List<BuildStep> _recipe;        // hardcoded in Inspector
[SerializeField] private AudioSource _successChime;      // reuse Sample 5 ReceiveResponse.wav

private int _currentStep;
private readonly List<Vector3> _placedPositions = new(); // world pos at moment of placement
```

Per-detection classification (called by the renderer for each detection each frame):

```csharp
public BrickHighlightState Classify(string className, Vector3 worldPos) {
    if (_currentStep >= _recipe.Count) return BrickHighlightState.Grey; // build complete
    var current = _recipe[_currentStep];
    if (className != current.brickClass) {
        return IsLegoClass(className) ? BrickHighlightState.Yellow : BrickHighlightState.Hidden;
    }
    // Right type. Spatial check.
    if (_currentStep == 0) return BrickHighlightState.Red; // candidate; not yet committed
    var lastPos = _placedPositions[_currentStep - 1];
    if (Vector3.Distance(worldPos, lastPos) <= current.proximityTolerance) {
        CommitStep(worldPos); // advances _currentStep, plays chime, returns Green for caller
        return BrickHighlightState.Green;
    }
    return BrickHighlightState.Red;
}
```

Step 1 commits when the user has held the right-type detection still for ≥ 1 s (debounce against flicker). Subsequent steps commit immediately on the first frame the proximity check passes — they are inherently debounced by the 10 Hz inference rate plus the requirement that the prior brick was already detected at `lastPos`.

`IsLegoClass()` returns `true` for any class in `LegoClasses.txt`, `false` otherwise — supports the case where the model also outputs non-brick classes (rare but possible).

**3. `LegoBrickHighlightRenderer.cs`** — replaces `ObjectRenderer.cs`. Reuses its detection-to-3D projection logic line-for-line (`ObjectRenderer.cs:60–96` — viewport conversion, raycast, surface-normal sampling). Differences:

- Drops the `labelFilters` array and the dedup-by-class-key dictionary (no longer needed).
- Spawns a `LegoHighlight` prefab from an existing `MarkerPool` (`Samples/Common/Scripts/MarkerPool.cs`).
- After each detection's world position is computed, calls `_buildManager.Classify(className, worldPos)` and forwards the result to `highlight.SetState(state)`.
- If state is `Hidden`, skip spawning entirely.

**4. `LegoBrickHighlight.cs`** — small component on the highlight prefab. Holds references to the four state materials in the Inspector. `SetState(BrickHighlightState)` swaps the `MeshRenderer.material` and triggers an auto-hide coroutine after 2 s (mirroring `MarkerController.cs:21–43`'s pattern). `Green` plays a brief scale pulse (LERP 1.0 → 1.2 → 1.0 over 0.4 s) before reverting to `Grey` and being added to `_placedPositions` by the manager.

### Critical files to **read** (reuse verbatim or near-verbatim)

- `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Scripts/ObjectDetector.cs` — fork as `LegoDetector.cs`
- `Unity-QuestVisionKit/Assets/Samples/2 ObjectDetection/Scripts/ObjectRenderer.cs` — fork as `LegoBrickHighlightRenderer.cs`; keep the viewport→ray→raycast pipeline (`DetectionToViewport`, surface-normal sampling)
- `Unity-QuestVisionKit/Assets/Samples/Common/Scripts/MarkerPool.cs` — reuse as-is for highlight pooling
- `Unity-QuestVisionKit/Assets/Samples/Common/Scripts/MarkerController.cs` — pattern for `LegoBrickHighlight.cs` (auto-hide timer)
- `Unity-QuestVisionKit/Assets/Samples/Common/Materials/Marker.mat` — clone four times, change `_BaseColor`, save as the four state materials
- `Unity-QuestVisionKit/Assets/Samples/5 ImageLLM/Audio/ReceiveResponse.wav` — assign to `_successChime` AudioSource

### Critical files to **create** (the only writes)

All under the new `Samples/2 ObjectDetection/Lego/` subfolder enumerated above. The existing `ObjectDetection.unity` scene and its scripts remain untouched.

### Out of scope

- Training the Lego YOLO model. The plan ships a placeholder `lego_yolo.sentis` and `LegoClasses.txt`; the user supplies real assets.
- Mistake handling beyond "wrong brick stays yellow / wrong-position right-brick stays red." No backtracking, no error sounds.
- Build-target anchoring / MRUK. Step 1 is presence-only, so the user implicitly anchors by placing the first brick wherever they like.
- Hand-tracking gestures, restart UI, multi-recipe selection.

## Verification

End-to-end test on a Quest 3 device (the editor cannot run PCA):

1. **Build & deploy.** File → Build Settings → Android → set `LegoAssembly.unity` as the active scene → Build and Run. Or `Meta → Build Android APK` and `adb install -r`.
2. **Compile-clean check.** Before deploying, in the editor: open `LegoAssembly.unity`, confirm no missing-reference warnings on `LegoBuildManager`, no null `MarkerPool` references on `LegoBrickHighlightRenderer`, and `Resources/LegoClasses.txt` is non-empty.
3. **Inspector configuration.** On the manager GameObject, set `_recipe` to a 3-step sequence using class names that exist in `LegoClasses.txt` (e.g., `red_2x4`, `blue_2x2`, `red_2x4`). Set `proximityTolerance` to `0.10` for each step.
4. **Detector smoke.** With at least one Lego brick of a known class in view, confirm via `adb logcat -s Unity` that `LegoDetector` emits `OnDetections` events at ~10 Hz with the expected class name.
5. **Color states.** With three distinct loose bricks on a table:
   - Brick matching step 1 → **red** outline.
   - Bricks matching steps 2 and 3 (not yet active) → **yellow**.
   - A non-Lego object (mug, pen) → no outline.
6. **Placement transition.** Hold the step-1 brick still for ~1 s. Expect a chime, a brief green pulse, and the outline to settle to **grey** (or fade out if occluded). The previously-yellow step-2 brick now becomes **red**.
7. **Spatial check.** With step-2's brick on the far side of the table from the placed step-1 brick (> 10 cm), the outline stays **red** (right type, wrong position) — no chime. Move it within 10 cm of step 1; it pulses **green** and step 3 becomes red.
8. **Completion.** After the final step commits, the chime plays once more and the manager goes idle (no further state changes; further detections render as grey).
9. **Regression check on the original sample.** Open `ObjectDetection.unity`, build, deploy, and confirm generic COCO detection still works as before — the fork must not have touched it.
