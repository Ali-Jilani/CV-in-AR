using System.Collections;
using UnityEngine;
using Meta.XR;
using System;

public class LegoDetector : MonoBehaviour
{
    [Header("Environment Sampling")]
    [SerializeField] private Unity.InferenceEngine.ModelAsset sentisModel;
    [SerializeField] private Unity.InferenceEngine.BackendType backend = Unity.InferenceEngine.BackendType.CPU;
    [SerializeField] private float inferenceInterval = 0.1f;
    [SerializeField] private int kLayersPerFrame = 20;

    private PassthroughCameraAccess _cameraAccess;
    private Unity.InferenceEngine.Model _model;
    private Unity.InferenceEngine.Worker _engine;
    private LegoBrickHighlightRenderer _highlightRenderer;
    private Coroutine _inferenceCoroutine;
    private Texture _cameraTexture;
    private const int InputSize = 640;

    private void Start()
    {
        _cameraAccess = GetComponent<PassthroughCameraAccess>();
        _highlightRenderer = GetComponent<LegoBrickHighlightRenderer>();

        if (!_cameraAccess || !_highlightRenderer)
        {
            Debug.LogError("[LegoDetector] PassthroughCameraAccess or LegoBrickHighlightRenderer not found.");
            return;
        }

        LoadModel();
        _inferenceCoroutine = StartCoroutine(InferenceLoop());
    }

    private void OnDestroy()
    {
        if (_inferenceCoroutine != null)
        {
            StopCoroutine(_inferenceCoroutine);
            _inferenceCoroutine = null;
        }

        _engine?.Dispose();
    }

    private void LoadModel()
    {
        try
        {
            _model = Unity.InferenceEngine.ModelLoader.Load(sentisModel);
            _engine = new Unity.InferenceEngine.Worker(_model, backend);
        }
        catch (Exception e)
        {
            Debug.LogError("[LegoDetector] Failed to load model: " + e.Message);
        }
    }

    private IEnumerator InferenceLoop()
    {
        while (isActiveAndEnabled)
        {
            if (!TryEnsureCameraTexture())
            {
                yield return null;
                continue;
            }

            yield return new WaitForSeconds(inferenceInterval);

            yield return StartCoroutine(PerformInference(_cameraTexture));
        }
    }

    private IEnumerator PerformInference(Texture texture)
    {
        var tensorShape = new Unity.InferenceEngine.TensorShape(1, 3, InputSize, InputSize);
        var inputTensor = new Unity.InferenceEngine.Tensor<float>(tensorShape);
        Unity.InferenceEngine.TextureConverter.ToTensor(texture, inputTensor);

        var schedule = _engine.ScheduleIterable(inputTensor);
        if (schedule == null)
        {
            Debug.LogWarning("[LegoDetector] ScheduleIterable returned null; falling back to synchronous scheduling.");
            _engine.Schedule(inputTensor);
        }
        else
        {
            var it = 0;
            while (schedule.MoveNext())
            {
                if (++it % kLayersPerFrame == 0)
                    yield return null;
            }
        }

        Unity.InferenceEngine.Tensor<float> coordsOutput = null;
        Unity.InferenceEngine.Tensor<int> labelIDsOutput = null;
        Unity.InferenceEngine.Tensor<float> confidenceOutput = null;
        Unity.InferenceEngine.Tensor<float> pullCoords = _engine.PeekOutput(0) as Unity.InferenceEngine.Tensor<float>;
        Unity.InferenceEngine.Tensor<int> pullLabelIDs = _engine.PeekOutput(1) as Unity.InferenceEngine.Tensor<int>;
        Unity.InferenceEngine.Tensor<float> pullConfidences = TryPeekConfidenceOutput();

        var isWaiting = false;
        var downloadState = 0;

        while (true)
        {
            switch (downloadState)
            {
                case 0:
                    if (pullCoords?.dataOnBackend == null)
                    {
                        Debug.LogError("[LegoDetector] Coordinates output is null or missing backend data.");
                        inputTensor.Dispose();
                        yield break;
                    }
                    if (!isWaiting)
                    {
                        pullCoords.ReadbackRequest();
                        isWaiting = true;
                    }
                    else if (pullCoords.IsReadbackRequestDone())
                    {
                        coordsOutput = pullCoords.ReadbackAndClone();
                        isWaiting = false;
                        downloadState = 1;
                    }
                    break;
                case 1:
                    if (pullLabelIDs?.dataOnBackend == null)
                    {
                        Debug.LogError("[LegoDetector] LabelIDs output is null or missing backend data.");
                        inputTensor.Dispose();
                        coordsOutput?.Dispose();
                        yield break;
                    }
                    if (!isWaiting)
                    {
                        pullLabelIDs.ReadbackRequest();
                        isWaiting = true;
                    }
                    else if (pullLabelIDs.IsReadbackRequestDone())
                    {
                        labelIDsOutput = pullLabelIDs.ReadbackAndClone();
                        isWaiting = false;
                        downloadState = pullConfidences != null ? 2 : 3;
                    }
                    break;
                case 2:
                    if (pullConfidences?.dataOnBackend == null)
                    {
                        pullConfidences = null;
                        downloadState = 3;
                        break;
                    }
                    if (!isWaiting)
                    {
                        pullConfidences.ReadbackRequest();
                        isWaiting = true;
                    }
                    else if (pullConfidences.IsReadbackRequestDone())
                    {
                        confidenceOutput = pullConfidences.ReadbackAndClone();
                        isWaiting = false;
                        downloadState = 3;
                    }
                    break;
                case 3:
                    if (_highlightRenderer)
                    {
                        _highlightRenderer.RenderDetections(
                            coordsOutput,
                            labelIDsOutput,
                            confidenceOutput
                        );
                    }
                    downloadState = 4;
                    break;
                case 4:
                    inputTensor.Dispose();
                    coordsOutput?.Dispose();
                    labelIDsOutput?.Dispose();
                    confidenceOutput?.Dispose();
                    yield break;
            }
            yield return null;
        }
    }

    private bool TryEnsureCameraTexture()
    {
        if (!_cameraAccess || !_cameraAccess.IsPlaying)
        {
            return false;
        }

        if (_cameraTexture)
        {
            return true;
        }

        _cameraTexture = _cameraAccess.GetTexture();
        if (_cameraTexture)
        {
            var resolution = _cameraAccess.CurrentResolution;
            print($"[LegoDetector] Passthrough texture ready: {resolution.x}x{resolution.y}");
        }
        else
        {
            Debug.LogWarning("[LegoDetector] Passthrough texture not available yet.");
        }

        return _cameraTexture;
    }

    private Unity.InferenceEngine.Tensor<float> TryPeekConfidenceOutput()
    {
        try
        {
            return _engine.PeekOutput(2) as Unity.InferenceEngine.Tensor<float>;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
