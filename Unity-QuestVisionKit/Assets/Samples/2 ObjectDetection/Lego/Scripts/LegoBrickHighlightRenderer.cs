using System;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR;

public class LegoBrickHighlightRenderer : MonoBehaviour
{
    [Header("Camera & Raycast Settings")]
    [SerializeField] private float mergeThreshold = 0.05f;

    [Header("Highlight")]
    [SerializeField] private GameObject highlightPrefab;

    [Header("Build Manager")]
    [SerializeField] private LegoBuildManager buildManager;

    [Header("Class Labels")]
    [Tooltip("Resource name (without extension) of the class label list. Each line is one class, in the order the model emits them.")]
    [SerializeField] private string classLabelsResource = "LegoClasses";

    [Header("Filtering")]
    [SerializeField, Range(0f, 1f)] private float minConfidence = 0.15f;

    [Header("Stabilisation")]
    [Tooltip("Per-detection-cycle lerp factor for highlight pose (0 = frozen, 1 = snap to detection). Lower values feel more anchored but lag behind real motion. Applied after the GO has been seen for at least one cycle.")]
    [SerializeField, Range(0f, 1f)] private float _highlightSmoothing = 0.25f;

    private Camera _mainCamera;
    private const float ModelInputSize = 640f;
    private PassthroughCameraAccess _cameraAccess;
    private EnvironmentRaycastManager _envRaycastManager;
    private string[] _labels = Array.Empty<string>();
    private readonly Dictionary<string, GameObject> _activeHighlights = new();
    private readonly HashSet<string> _seenThisCycle = new();

    private void Awake()
    {
        _cameraAccess = GetComponent<PassthroughCameraAccess>() ?? FindAnyObjectByType<PassthroughCameraAccess>(FindObjectsInactive.Include);
        _envRaycastManager = GetComponent<EnvironmentRaycastManager>() ?? FindAnyObjectByType<EnvironmentRaycastManager>(FindObjectsInactive.Include);
        if (!_cameraAccess || !_envRaycastManager)
        {
            Debug.LogWarning("[LegoBrickHighlightRenderer] Passthrough camera or Environment Raycast Manager is not ready.");
            return;
        }
        _mainCamera = Camera.main;

        LoadLabels();
        if (buildManager)
        {
            buildManager.RegisterLegoClasses(_labels);
        }
    }

    private void LoadLabels()
    {
        var asset = Resources.Load<TextAsset>(classLabelsResource);
        if (asset == null)
        {
            Debug.LogError($"[LegoBrickHighlightRenderer] Could not load class labels from Resources/{classLabelsResource}.txt");
            return;
        }
        _labels = asset.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < _labels.Length; i++)
        {
            _labels[i] = _labels[i].Trim();
        }
    }

    public void RenderDetections(Unity.InferenceEngine.Tensor<float> coords, Unity.InferenceEngine.Tensor<int> labelIDs, Unity.InferenceEngine.Tensor<float> confidences = null)
    {
        if (coords == null || labelIDs == null)
        {
            return;
        }

        if (!_cameraAccess || !_envRaycastManager || !buildManager)
        {
            Debug.LogWarning("[LegoBrickHighlightRenderer] Missing dependencies.");
            return;
        }

        var numDetections = coords.shape[0];
        _seenThisCycle.Clear();

        var imageWidth = ModelInputSize;
        var imageHeight = ModelInputSize;
        var halfWidth = imageWidth * 0.5f;
        var halfHeight = imageHeight * 0.5f;

        for (var i = 0; i < numDetections; i++)
        {
            var detectedCenterX = coords[i, 0];
            var detectedCenterY = coords[i, 1];
            var detectedWidth = coords[i, 2];
            var detectedHeight = coords[i, 3];

            var adjustedCenterX = detectedCenterX - halfWidth;
            var adjustedCenterY = detectedCenterY - halfHeight;

            var perX = (adjustedCenterX + halfWidth) / imageWidth;
            var perY = (adjustedCenterY + halfHeight) / imageHeight;
            var centerRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(perX, perY));

            if (!_envRaycastManager.Raycast(centerRay, out var centerHit))
            {
                continue;
            }

            var worldPos = centerHit.point;

            var u1 = (detectedCenterX - detectedWidth * 0.5f) / imageWidth;
            var v1 = (detectedCenterY - detectedHeight * 0.5f) / imageHeight;
            var u2 = (detectedCenterX + detectedWidth * 0.5f) / imageWidth;
            var v2 = (detectedCenterY + detectedHeight * 0.5f) / imageHeight;

            var tlRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u1, v1));
            var trRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u2, v1));
            var blRay = _cameraAccess.ViewportPointToRay(DetectionToViewport(u1, v2));

            var depth = Vector3.Distance(_mainCamera.transform.position, worldPos);
            var worldTL = tlRay.GetPoint(depth);
            var worldTR = trRay.GetPoint(depth);
            var worldBL = blRay.GetPoint(depth);

            var width = Vector3.Distance(worldTR, worldTL);
            var height = Vector3.Distance(worldBL, worldTL);
            var scale = new Vector3(width, height, 1f);

            var labelIndex = labelIDs[i];
            var className = ResolveClass(labelIndex);
            if (string.IsNullOrEmpty(className))
            {
                continue;
            }

            var confidence = GetConfidence(coords, confidences, i);
            if (confidence >= 0f && confidence < minConfidence)
            {
                continue;
            }

            var state = buildManager.Classify(className, worldPos);
            if (state == BrickHighlightState.Hidden)
            {
                continue;
            }

            var surfaceNormal = SampleSurfaceNormal(worldPos, centerHit.normal);
            var rotation = Quaternion.LookRotation(-surfaceNormal, Vector3.up);

            var lookupKey = className;
            if (_seenThisCycle.Contains(lookupKey))
            {
                if (_activeHighlights.TryGetValue(lookupKey, out var primaryGo) && primaryGo != null
                    && Vector3.Distance(primaryGo.transform.position, worldPos) < mergeThreshold)
                {
                    continue;
                }
                lookupKey = $"{className}_{i}";
            }

            var hadExisting = _activeHighlights.TryGetValue(lookupKey, out var highlightGo) && highlightGo != null;
            var snap = !hadExisting || !highlightGo.activeSelf;
            if (!hadExisting)
            {
                highlightGo = Instantiate(highlightPrefab);
                _activeHighlights[lookupKey] = highlightGo;
            }
            if (!highlightGo.activeSelf)
            {
                highlightGo.SetActive(true);
            }
            UpdateHighlight(highlightGo, worldPos, rotation, scale, state, snap);
            _seenThisCycle.Add(lookupKey);
        }

        foreach (var kv in _activeHighlights)
        {
            if (kv.Value && !_seenThisCycle.Contains(kv.Key) && kv.Value.activeSelf)
            {
                kv.Value.SetActive(false);
            }
        }
    }

    private void UpdateHighlight(GameObject go, Vector3 position, Quaternion rotation, Vector3 scale, BrickHighlightState state, bool snap)
    {
        if (snap || _highlightSmoothing >= 1f)
        {
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
        }
        else
        {
            var t = Mathf.Clamp01(_highlightSmoothing);
            go.transform.position = Vector3.Lerp(go.transform.position, position, t);
            go.transform.rotation = Quaternion.Slerp(go.transform.rotation, rotation, t);
            go.transform.localScale = Vector3.Lerp(go.transform.localScale, scale, t);
        }
        var highlight = go.GetComponent<LegoBrickHighlight>();
        if (highlight)
        {
            highlight.SetState(state);
        }
    }

    private string ResolveClass(int labelIndex)
    {
        if (_labels.Length == 0)
        {
            return labelIndex.ToString();
        }
        if (labelIndex < 0 || labelIndex >= _labels.Length)
        {
            return string.Empty;
        }
        return _labels[labelIndex];
    }

    private void OnDisable()
    {
        foreach (var kv in _activeHighlights)
        {
            if (kv.Value && kv.Value.activeSelf)
            {
                kv.Value.SetActive(false);
            }
        }
        _seenThisCycle.Clear();
    }

    private void OnDestroy()
    {
        foreach (var kv in _activeHighlights)
        {
            if (kv.Value)
            {
                Destroy(kv.Value);
            }
        }
        _activeHighlights.Clear();
        _seenThisCycle.Clear();
    }

    private Vector2 DetectionToViewport(float normalizedX, float normalizedY)
    {
        var resolution = (Vector2)_cameraAccess.CurrentResolution;
        if (resolution == Vector2.zero)
        {
            resolution = (Vector2)_cameraAccess.Intrinsics.SensorResolution;
        }
        if (resolution == Vector2.zero)
        {
            return new Vector2(Mathf.Clamp01(normalizedX), Mathf.Clamp01(1f - normalizedY));
        }

        var scaledX = Mathf.Clamp01(normalizedX) * ModelInputSize;
        var scaledY = Mathf.Clamp01(normalizedY) * ModelInputSize;

        var actualPixel = new Vector2(
            scaledX * (resolution.x / ModelInputSize),
            scaledY * (resolution.y / ModelInputSize));

        return new Vector2(
            Mathf.Clamp01(actualPixel.x / resolution.x),
            Mathf.Clamp01(1f - actualPixel.y / resolution.y));
    }

    private static float GetConfidence(Unity.InferenceEngine.Tensor<float> coords, Unity.InferenceEngine.Tensor<float> confidenceTensor, int index)
    {
        var sampled = SampleConfidence(confidenceTensor, index);
        if (sampled >= 0f)
        {
            return Mathf.Clamp01(sampled);
        }

        if (coords == null || coords.shape.rank < 2)
        {
            return -1f;
        }

        var channels = coords.shape[coords.shape.rank - 1];
        if (channels <= 4)
        {
            return -1f;
        }

        try
        {
            return Mathf.Clamp01(coords[index, 4]);
        }
        catch (Exception)
        {
            return -1f;
        }
    }

    private static float SampleConfidence(Unity.InferenceEngine.Tensor<float> tensor, int index)
    {
        if (tensor == null)
        {
            return -1f;
        }

        var length = tensor.shape.length;
        if (index < 0 || index >= length)
        {
            return -1f;
        }

        try
        {
            return tensor[index];
        }
        catch
        {
            return -1f;
        }
    }

    private Vector3 SampleSurfaceNormal(Vector3 position, Vector3 fallbackNormal)
    {
        if (_envRaycastManager == null)
        {
            return fallbackNormal;
        }

        var origin = _mainCamera ? _mainCamera.transform.position : position - fallbackNormal * 0.1f;
        var direction = position - origin;
        if (direction.sqrMagnitude > 0.0001f)
        {
            if (_envRaycastManager.Raycast(new Ray(origin, direction.normalized), out var hit, direction.magnitude + 0.05f))
            {
                return hit.normal;
            }
        }

        var offsetOrigin = position + fallbackNormal.normalized * 0.05f;
        if (_envRaycastManager.Raycast(new Ray(offsetOrigin, -fallbackNormal.normalized), out var reverseHit, 0.2f))
        {
            return reverseHit.normal;
        }

        return fallbackNormal;
    }
}
