using System.IO;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;

public static class YoloOnnxToSentisWrapper
{
    private const string SourceAssetPath =
        "Assets/Samples/2 ObjectDetection/Lego/Resources/blocks.onnx";

    private const string DestinationAssetPath =
        "Assets/Samples/2 ObjectDetection/Lego/Resources/blocks.sentis";

    private const int InputSize = 640;
    private const int NumClasses = 5;
    private const float IouThreshold = 0.45f;
    private const float ScoreThreshold = 0.25f;

    [MenuItem("Tools/Lego/Wrap YOLOv11 ONNX with NMS")]
    public static void Wrap()
    {
        var src = AssetDatabase.LoadAssetAtPath<ModelAsset>(SourceAssetPath);
        if (src == null)
        {
            Debug.LogError($"[YoloOnnxToSentisWrapper] Could not load ModelAsset at {SourceAssetPath}");
            return;
        }

        var sourceModel = ModelLoader.Load(src);
        if (sourceModel == null)
        {
            Debug.LogError("[YoloOnnxToSentisWrapper] ModelLoader.Load returned null.");
            return;
        }

        var graph = new FunctionalGraph();
        var inputs = graph.AddInputs(sourceModel);
        var rawOutputs = Functional.Forward(sourceModel, inputs);
        if (rawOutputs == null || rawOutputs.Length == 0)
        {
            Debug.LogError("[YoloOnnxToSentisWrapper] Forward produced no outputs.");
            return;
        }

        var raw = rawOutputs[0];
        var t = raw.Squeeze(new[] { 0 }).Transpose(0, 1);

        var boxesCxCyWh = t.Narrow(1, 0, 4);
        var scores = t.Narrow(1, 4, NumClasses);

        var classIds = Functional.ArgMax(scores, dim: 1);
        var maxScores = Functional.ReduceMax(scores, dim: 1);

        var cx = boxesCxCyWh.Narrow(1, 0, 1);
        var cy = boxesCxCyWh.Narrow(1, 1, 1);
        var w = boxesCxCyWh.Narrow(1, 2, 1);
        var h = boxesCxCyWh.Narrow(1, 3, 1);
        var halfW = w * 0.5f;
        var halfH = h * 0.5f;
        var x1 = (cx - halfW) / (float)InputSize;
        var y1 = (cy - halfH) / (float)InputSize;
        var x2 = (cx + halfW) / (float)InputSize;
        var y2 = (cy + halfH) / (float)InputSize;
        var xyxy = Functional.Concat(new[] { x1, y1, x2, y2 }, dim: 1);

        var keptIdx = Functional.NMS(xyxy, maxScores, IouThreshold, ScoreThreshold);

        var keptBoxes = boxesCxCyWh.IndexSelect(0, keptIdx);
        var keptLabels = classIds.IndexSelect(0, keptIdx);
        var keptScores = maxScores.IndexSelect(0, keptIdx);

        graph.AddOutput(keptBoxes, "coords");
        graph.AddOutput(keptLabels, "labelIDs");
        graph.AddOutput(keptScores, "confidences");

        var compiled = graph.Compile();

        var absPath = Path.GetFullPath(DestinationAssetPath);
        ModelWriter.Save(absPath, compiled);
        AssetDatabase.ImportAsset(DestinationAssetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[YoloOnnxToSentisWrapper] Wrote {DestinationAssetPath} (iou={IouThreshold}, score={ScoreThreshold})");
    }
}
