using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class GenerateLegoMonoFontAsset
{
    private const string TtfPath = "Assets/Samples/2 ObjectDetection/Lego/Resources/Fonts/RobotoMono-Regular.ttf";
    private const string AssetPath = "Assets/Samples/2 ObjectDetection/Lego/Resources/Fonts/RobotoMono-Regular SDF.asset";

    [MenuItem("Tools/Lego/Regenerate Mono TMP Font Asset")]
    public static void Regenerate()
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (ttf == null)
        {
            Debug.LogError($"[GenerateLegoMonoFontAsset] TTF not found at {TtfPath}");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath) != null)
        {
            AssetDatabase.DeleteAsset(AssetPath);
        }

        var fontAsset = TMP_FontAsset.CreateFontAsset(
            ttf,
            samplingPointSize: 90,
            atlasPadding: 9,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 1024,
            atlasHeight: 1024,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);
        if (fontAsset == null)
        {
            Debug.LogError("[GenerateLegoMonoFontAsset] CreateFontAsset returned null");
            return;
        }
        fontAsset.name = "RobotoMono-Regular SDF";
        AssetDatabase.CreateAsset(fontAsset, AssetPath);

        if (fontAsset.atlasTextures != null)
        {
            foreach (var atlas in fontAsset.atlasTextures)
            {
                if (atlas == null) continue;
                if (!AssetDatabase.IsSubAsset(atlas))
                {
                    AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                }
            }
        }
        if (fontAsset.material != null && !AssetDatabase.IsSubAsset(fontAsset.material))
        {
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GenerateLegoMonoFontAsset] Regenerated TMP Font Asset at {AssetPath}");
    }
}
