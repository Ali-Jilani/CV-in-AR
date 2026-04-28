# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

QuestCameraKit is a collection of Unity sample projects demonstrating Meta Quest's **Passthrough Camera API (PCA)** for AR/VR computer vision, tracking, and shader effects. It targets **Meta Quest 3/3s** with **HorizonOS v74+**.

## Build & Run

- **Engine:** Unity 6 (6000.2.8f1), also compatible with Unity 2022.3 LTS
- **Platform:** Android (ARM64) via Meta Quest
- **Render Pipeline:** Universal Render Pipeline (URP)
- **Build:** File > Build Settings > Android, or Meta > Build Android APK
- **Deploy:** Connect Quest via USB, use `adb install` or Meta Quest Hub
- **Editor limitation:** Passthrough Camera API is device-only; it does not work in the Unity Editor

There is no command-line build, test runner, or linter. All building and testing is done through the Unity Editor and on-device.

## Architecture

### Sample Structure

Each sample is self-contained under `Unity-QuestVisionKit/Assets/Samples/`:

| # | Sample | Key Script(s) | Dependencies |
|---|--------|---------------|-------------|
| 1 | ColorPicker | `ColorPicker.cs` | MRUK EnvironmentRaycastManager |
| 2 | ObjectDetection | `ObjectDetector.cs`, `ObjectRenderer.cs` | Unity Inference Engine (`com.unity.ai.inference@2.3.0`), YOLOv9 |
| 3 | QRCodeTracking | `QrCodeScanner.cs`, `Downsample.compute` | ZXing.Net (`#if ZXING_ENABLED`) |
| 4 | Shaders | `StereoCameraMappingController.cs`, 3 `.shader` files | URP, PCA textures |
| 5 | ImageLLM | `ImageOpenAIConnector.cs`, `STTManager.cs`, `TTSManager.cs` | OpenAI API key, Oculus Voice SDK |
| 6 | WebRTC | `WebRTCController.cs` | SimpleWebRTC (`#if WEBRTC_ENABLED`), NativeWebSocket |
| 7 | QRCodeDetection | `QRCodeManager.cs` | Meta MRUK (experimental headset mode required) |

Shared utilities live in `Samples/Common/` (MarkerPool, MarkerController, Marker prefab).

### Conditional Compilation

Some samples use define symbols that are auto-added by editor scripts:
- `ZXING_ENABLED` - set by `ZXingDefineSymbolChecker.cs` when ZXing DLLs are present
- `WEBRTC_ENABLED` - set by `WebRTCDefineSymbolChecker.cs` when SimpleWebRTC is installed

### Stereo Passthrough Shader Pipeline

The shaders in Sample 4 implement per-eye camera reprojection using real-time intrinsics:
1. `StereoCameraMappingController.cs` extracts camera pose, intrinsics, and textures from PCA each frame
2. Shaders receive per-eye data (focal length, principal point, sensor resolution, rotation matrix, UV offsets)
3. Fragment shaders project world positions through pinhole camera model to sample the correct texel
4. Three variants: direct mapping, frosted glass (blur + refraction noise), wavy portal (wave distortion)

Shader properties use `_PascalCase` naming (e.g., `_LeftTex`, `_FocalLength`). Static shader IDs use `Shader.PropertyToID` for performance.

### Key Packages (from manifest.json)

- `com.meta.xr.mrutilitykit@83.0.4` - MR Utility Kit (environment raycasting, scene understanding)
- `com.meta.xr.sdk.core@83.0.4` - Meta XR Core SDK
- `com.unity.ai.inference@2.3.0` - ML inference (CPU/GPU)
- `com.unity.render-pipelines.universal@17.2.0` - URP
- `com.unity.xr.openxr@1.15.1` / `com.unity.xr.meta-openxr@2.2.1` - OpenXR runtime

### Android Permissions (AndroidManifest.xml)

Critical permissions: `horizonos.permission.HEADSET_CAMERA` (PCA), `com.oculus.permission.HAND_TRACKING`, `com.oculus.permission.USE_SCENE`, `com.oculus.permission.USE_ANCHOR_API`.

## Code Conventions

- **MonoBehaviour-based** component architecture, one manager script per sample scene
- **Private fields:** `_camelCase`
- **Coroutine-heavy** for async operations (waiting for camera feed, inference results)
- **Object pooling** pattern used for markers (`MarkerPool.cs`)
- Camera/singleton resolution via `FindAnyObjectByType<T>()`
- Samples prioritize simplicity and readability so developers can quickly adapt them

## Contributing

- Fork from `main`, test on Quest 3/3s with HorizonOS v74+
- Keep samples simple and easy to understand
- Each scene should be independently runnable
